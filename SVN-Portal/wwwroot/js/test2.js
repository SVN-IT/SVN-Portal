/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 * customscript_svn_pr_po_mr
 *
 * ─────────────────────────────────────────────────────────────────
 * PHƯƠNG ÁN 5 — chạy tạo PO ở background thay vì đồng bộ trong Suitelet.
 *
 * Input: 1 customrecord_pr_po_job (đã được Suitelet tạo sẵn kèm các
 * customrecord_pr_po_job_line — mỗi line ứng với 1 pool item user đã chọn
 * vendor/giá trên form Create PO).
 *
 * getInputData → group job_line theo vendor (mỗi vendor = 1 PO = 1 map key)
 * map           → tạo PO thật cho 1 vendor (mix nhiều pool item / nhiều PR
 *                 nguồn), dọn pool, ghi item history/price log, rồi emit
 *                 key=prId để reduce gom update PR.
 * reduce        → load + save MỖI PR ĐÚNG 1 LẦN (giữ nguyên tinh thần Fix #4,
 *                 nhưng giờ do framework tự gom theo key).
 * summarize     → build file .txt tóm tắt, lưu vào File Cabinet, cập nhật
 *                 trạng thái job, gửi email cho người bấm Create PO.
 *
 * ⚠️ IDEMPOTENCY: NetSuite có thể tự retry 1 map/reduce key nếu instance đó
 * lỗi/timeout — khác với Suitelet chạy 1 lần duy nhất. Vì vậy:
 *   - map() kiểm tra job_line.status trước khi tạo PO — nếu đã "done" thì
 *     BỎ QUA việc tạo PO (tránh tạo trùng PO), chỉ emit lại dữ liệu cho reduce.
 *   - Xóa pool_item/pool_source cũng guard: nếu record đã bị xóa (do lần
 *     chạy trước) thì bỏ qua lỗi "record not found", không coi là fail.
 * ─────────────────────────────────────────────────────────────────
 */
define(["N/record", "N/search", "N/file", "N/email", "N/runtime", "N/log"],
    function (record, search, file, email, runtime, log) {

        var TAX_VAT_0 = 6234;
        var SUMMARY_FOLDER_ID = 104606; // dùng chung folder với file PR đã ký — đổi nếu cần folder riêng

        function formatNumber(num) {
            num = Math.round(Number(num) || 0);
            var sign = num < 0 ? "-" : "";
            var absStr = Math.abs(num).toString();
            absStr = absStr.replace(/\B(?=(\d{3})+(?!\d))/g, ",");
            return sign + absStr;
        }

        // ───────────────────────── getInputData ─────────────────────────
        function getInputData(context) {
            var script = runtime.getCurrentScript();
            var jobId = script.getParameter({ name: "custscript_ppj_jobid" });
            if (!jobId) throw new Error("Thiếu tham số custscript_ppj_jobid khi trigger Map/Reduce.");

            // Chỉ lấy các job_line CHƯA xử lý xong — nhờ vậy nếu job bị chạy lại
            // (retry cả getInputData, hoặc bạn tự resume sau này) thì các dòng đã
            // "done" sẽ không bị đưa vào lại.
            var lineIdsByVendor = {}; // vendorId -> [job_line internalid, ...]
            search.create({
                type: "customrecord_pr_po_job_line",
                filters: [
                    ["custrecord_ppjl_job", "is", jobId],
                    "AND",
                    ["custrecord_ppjl_status", "isnot", "done"]
                ],
                columns: ["internalid", "custrecord_ppjl_vendor"]
            }).run().each(function (r) {
                var vId = r.getValue("custrecord_ppjl_vendor");
                var lId = r.getValue("internalid");
                if (!vId) return true; // dòng lỗi thiếu vendor — bỏ qua, đã được cảnh báo ở Suitelet trước khi tạo job
                if (!lineIdsByVendor[vId]) lineIdsByVendor[vId] = [];
                lineIdsByVendor[vId].push(lId);
                return true;
            });

            var groups = [];
            for (var vId2 in lineIdsByVendor) {
                groups.push({ jobId: jobId, vendorId: vId2, lineIds: lineIdsByVendor[vId2] });
            }

            if (groups.length === 0) {
                log.audit("NO PENDING LINES", "Job " + jobId + " không còn dòng nào cần xử lý.");
            }
            return groups;
        }

        // ───────────────────────────── map ──────────────────────────────
        // context.value = 1 group JSON: { jobId, vendorId, lineIds }
        function map(context) {
            var group = JSON.parse(context.value);
            var jobId = group.jobId;
            var vendorId = group.vendorId;

            // Load chi tiết job_line ngay tại đây (không mang theo từ getInputData
            // để tránh dữ liệu cũ nếu có thay đổi, và giữ payload getInputData nhẹ).
            var lines = [];
            search.create({
                type: "customrecord_pr_po_job_line",
                filters: [["internalid", "anyof", group.lineIds]],
                columns: [
                    "internalid", "custrecord_ppjl_poolitem", "custrecord_ppjl_price",
                    "custrecord_ppjl_itemname", "custrecord_ppjl_qty", "custrecord_ppjl_origqty",
                    "custrecord_ppjl_status", "custrecord_ppjl_poid"
                ]
            }).run().each(function (r) {
                lines.push({
                    id: r.getValue("internalid"),
                    poolItemId: r.getValue("custrecord_ppjl_poolitem"),
                    price: parseFloat(r.getValue("custrecord_ppjl_price")) || 0,
                    itemName: (r.getValue("custrecord_ppjl_itemname") || "").trim(),
                    qty: parseFloat(r.getValue("custrecord_ppjl_qty")) || 0,
                    origQty: parseFloat(r.getValue("custrecord_ppjl_origqty")) || 0,
                    status: r.getValue("custrecord_ppjl_status"),
                    poId: r.getValue("custrecord_ppjl_poid")
                });
                return true;
            });
            if (lines.length === 0) return;

            // ── IDEMPOTENCY GUARD: nếu TẤT CẢ line trong group này đã "done" (đã
            // có poId) rồi — nghĩa là map key này đã chạy thành công ở lần trước,
            // đang bị NetSuite retry vì lý do khác (VD lỗi ở 1 map key khác cùng
            // job). KHÔNG tạo PO lại — chỉ re-emit dữ liệu cho reduce để đảm bảo
            // PR vẫn được cập nhật dù reduce có bị chạy lại. ──
            var allDone = lines.every(function (l) { return l.status === "done" && l.poId; });
            if (allDone) {
                try {
                    search.lookupFields({ type: record.Type.PURCHASE_ORDER, id: lines[0].poId, columns: ["tranid"] });
                    log.audit("SKIP - GROUP ALREADY DONE", "vendor " + vendorId + " | job " + jobId);
                    emitPrUpdatesFromDoneLines(context, lines); // vẫn nên implement thật, không để trống
                    return;
                } catch (poGoneErr) {
                    log.audit("PO ALREADY DELETED - REPROCESS", "vendor " + vendorId + " | job " + jobId);
                    // rơi xuống xử lý bình thường như group chưa done
                }
            }

            var poolFormIndexes = lines; // giữ tên biến gần giống code Suitelet cũ cho dễ đối chiếu

            var vendorLabelForSummary = vendorId;
            try {
                var vLookup = search.lookupFields({
                    type: search.Type.VENDOR, id: vendorId, columns: ["entityid", "companyname"]
                });
                vendorLabelForSummary = vLookup.entityid + (vLookup.companyname ? " — " + vLookup.companyname : "");
            } catch (vErr) {
                log.error("VENDOR LOOKUP ERROR", vErr);
            }

            var po = record.create({ type: record.Type.PURCHASE_ORDER, isDynamic: true });
            po.setValue({ fieldId: "customform", value: "252" });
            po.setValue({ fieldId: "entity", value: vendorId });

            var vendorLookup = search.lookupFields({
                type: search.Type.VENDOR,
                id: vendorId,
                columns: ["subsidiary"]
            });
            if (vendorLookup.subsidiary && vendorLookup.subsidiary.length > 0) {
                po.setValue({ fieldId: "subsidiary", value: vendorLookup.subsidiary[0].value });
            }

            var prUpdateQueue = [];
            var summaryLinesForVendor = [];
            var sourceIdsByPoolItem = {}; // để xóa pool_source ngay dưới, không search lại

            for (var k = 0; k < poolFormIndexes.length; k++) {
                var ln = poolFormIndexes[k];

                var poolItemLookup;
                try {
                    poolItemLookup = search.lookupFields({
                        type: "customrecord_pr_pool_item", id: ln.poolItemId,
                        columns: ["name", "custrecord_ppi_realitem"]
                    });
                } catch (lookupErr) {
                    // pool item đã bị xóa trước đó (VD retry sau khi đã xử lý xong) —
                    // coi như dòng này không còn gì để làm, đánh dấu skip.
                    log.audit("POOL ITEM MISSING - SKIP LINE", "poolItemId=" + ln.poolItemId);
                    markJobLineError(ln.id, "Pool item không còn tồn tại (có thể đã xử lý ở lần chạy trước)");
                    continue;
                }

                var poolName = ln.itemName || poolItemLookup.name;
                var realItemId = poolItemLookup.custrecord_ppi_realitem && poolItemLookup.custrecord_ppi_realitem[0]
                    ? poolItemLookup.custrecord_ppi_realitem[0].value : "";

                var sourceResults = search.create({
                    type: "customrecord_pr_pool_source",
                    filters: [["custrecord_pps_pool_item", "is", ln.poolItemId]],
                    columns: [
                        "internalid", "custrecord_pps_pr", "custrecord_pps_prlinenum",
                        "custrecord_pps_prlineidx", "custrecord_pps_qty", "custrecord_pps_desc",
                        "custrecord_pps_purpose", "custrecord_pps_units"
                    ]
                }).run().getRange({ start: 0, end: 1000 });

                var srcIds = [];
                for (var si = 0; si < sourceResults.length; si++) srcIds.push(sourceResults[si].getValue("internalid"));
                sourceIdsByPoolItem[ln.poolItemId] = srcIds;

                var editedQtyTotal = ln.qty;
                var origQtyTotal = ln.origQty;
                var qtyWasChanged = origQtyTotal > 0 && Math.abs(editedQtyTotal - origQtyTotal) > 0.0001;

                summaryLinesForVendor.push({
                    itemName: poolName, qty: editedQtyTotal, price: ln.price,
                    amount: Math.round(ln.price * editedQtyTotal * 100) / 100
                });

                var runningAssignedQty = 0;
                for (var s = 0; s < sourceResults.length; s++) {
                    var srcPrId = sourceResults[s].getValue("custrecord_pps_pr");
                    var srcLineNum = sourceResults[s].getValue("custrecord_pps_prlinenum");
                    var srcLineIdx = sourceResults[s].getValue("custrecord_pps_prlineidx");
                    var srcQty = parseFloat(sourceResults[s].getValue("custrecord_pps_qty")) || 0;
                    var srcDesc = sourceResults[s].getValue("custrecord_pps_desc");
                    var srcPurpose = sourceResults[s].getValue("custrecord_pps_purpose");
                    var srcUnits = sourceResults[s].getValue("custrecord_pps_units");

                    var lineQty;
                    if (!qtyWasChanged) {
                        lineQty = srcQty;
                    } else if (s === sourceResults.length - 1) {
                        lineQty = Math.round(editedQtyTotal - runningAssignedQty);
                    } else {
                        lineQty = Math.round((srcQty / origQtyTotal) * editedQtyTotal);
                        runningAssignedQty += lineQty;
                    }
                    if (isNaN(lineQty) || lineQty < 0) lineQty = srcQty;

                    po.selectNewLine({ sublistId: "item" });

                    // 1. BẮT BUỘC Set Item trước
                    if (realItemId) {
                        po.setCurrentSublistValue({ sublistId: "item", fieldId: "item", value: realItemId });
                    }

                    // 2. Set Units & Quantity (Dynamic mode cần Units trước Quantity nếu dùng Multiple Units)
                    if (srcUnits) {
                        try { po.setCurrentSublistValue({ sublistId: "item", fieldId: "units", value: srcUnits }); } catch (uErr) { }
                    }
                    po.setCurrentSublistValue({ sublistId: "item", fieldId: "quantity", value: lineQty });

                    // 3. Set Tax Code & Rate
                    // Gán Tax Code VAT 0% (biến TAX_VAT_0 = 6234 của bạn ở đầu file)
                    try {
                        po.setCurrentSublistValue({ sublistId: "item", fieldId: "taxcode", value: TAX_VAT_0 });
                    } catch (tErr) {
                        log.error("SET TAXCODE ERR", tErr);
                    }
                    po.setCurrentSublistValue({ sublistId: "item", fieldId: "rate", value: ln.price });

                    // 4. Set các Custom/Reference Fields khác
                    if (srcDesc) po.setCurrentSublistValue({ sublistId: "item", fieldId: "description", value: srcDesc });
                    po.setCurrentSublistValue({ sublistId: "item", fieldId: "custcol_pr_item", value: poolName || "" });
                    if (srcPurpose) po.setCurrentSublistValue({ sublistId: "item", fieldId: "custcol_pr_item_purpose", value: srcPurpose });
                    po.setCurrentSublistValue({ sublistId: "item", fieldId: "orderdoc", value: parseInt(srcPrId, 10) });
                    po.setCurrentSublistValue({ sublistId: "item", fieldId: "orderline", value: parseInt(srcLineNum, 10) });

                    // 5. Commit Line
                    po.commitLine({ sublistId: "item" });

                    prUpdateQueue.push({
                        prId: srcPrId, lineIdx: parseInt(srcLineIdx, 10), itemName: poolName,
                        qty: lineQty, amount: Math.round(ln.price * lineQty * 100) / 100
                    });
                }

                ln.__poolName = poolName; // giữ lại để dùng ở bước ghi history/dọn pool bên dưới
            }

            var poId;
            try {
                poId = po.save({ ignoreMandatoryFields: true });
            } catch (saveErr) {
                log.error("PO SAVE ERROR - VENDOR " + vendorId, saveErr);
                for (var e1 = 0; e1 < poolFormIndexes.length; e1++) markJobLineError(poolFormIndexes[e1].id, "Lỗi tạo PO: " + saveErr.message);
                throw saveErr; // để M/R ghi nhận map key này fail, có thể retry
            }

            for (var pq = 0; pq < prUpdateQueue.length; pq++) {
                prUpdateQueue[pq].poId = poId;
            }

            var poTranId = poId;
            try {
                var poLookup = search.lookupFields({ type: record.Type.PURCHASE_ORDER, id: poId, columns: ["tranid"] });
                if (poLookup.tranid) poTranId = poLookup.tranid;
            } catch (poLookupErr) { log.error("PO TRANID LOOKUP ERROR", poLookupErr); }

            // Đánh dấu job_line = done + ghi poId/poTranId/amount — QUAN TRỌNG cho idempotency
            // và cho summarize() build file tóm tắt sau này.
            for (var k2 = 0; k2 < poolFormIndexes.length; k2++) {
                var ln2 = poolFormIndexes[k2];
                if (!ln2.__poolName) continue; // dòng bị skip ở trên (pool item missing)
                var lineAmount = 0;
                for (var sl = 0; sl < summaryLinesForVendor.length; sl++) {
                    if (summaryLinesForVendor[sl].itemName === ln2.__poolName) { lineAmount = summaryLinesForVendor[sl].amount; break; }
                }
                try {
                    record.submitFields({
                        type: "customrecord_pr_po_job_line", id: ln2.id,
                        values: {
                            custrecord_ppjl_status: "done",
                            custrecord_ppjl_poid: poId,
                            custrecord_ppjl_potranid: String(poTranId),
                            custrecord_ppjl_vendorlabel: vendorLabelForSummary,
                            custrecord_ppjl_amount: lineAmount
                        }
                    });
                } catch (markErr) { log.error("MARK JOB LINE DONE ERROR", markErr); }
            }

            // Ghi item history + price log (giữ nguyên nghiệp vụ cũ)
            for (var k3 = 0; k3 < poolFormIndexes.length; k3++) {
                var ln3 = poolFormIndexes[k3];
                if (!ln3.__poolName) continue;
                try {
                    var foundHist = search.create({
                        type: "customrecord_pr_item_history",
                        filters: [["name", "is", ln3.__poolName]], columns: ["internalid"]
                    }).run().getRange({ start: 0, end: 1 });

                    var itemHistoryId;
                    if (foundHist.length > 0) {
                        itemHistoryId = foundHist[0].getValue("internalid");
                        record.submitFields({
                            type: "customrecord_pr_item_history", id: itemHistoryId,
                            values: { custrecord_pr_item_last_vendor: vendorId, custrecord_pr_item_last_purchase_price: ln3.price }
                        });
                    } else {
                        var newHist = record.create({ type: "customrecord_pr_item_history" });
                        newHist.setValue({ fieldId: "name", value: ln3.__poolName });
                        newHist.setValue({ fieldId: "custrecord_pr_item_last_vendor", value: vendorId });
                        newHist.setValue({ fieldId: "custrecord_pr_item_last_purchase_price", value: ln3.price });
                        itemHistoryId = newHist.save();
                    }

                    var logRec = record.create({ type: "customrecord_pr_item_price_log" });
                    logRec.setValue({ fieldId: "name", value: ln3.__poolName + " - " + (new Date()).toLocaleDateString() });
                    logRec.setValue({ fieldId: "custrecord_pil_item", value: itemHistoryId });
                    logRec.setValue({ fieldId: "custrecord_pil_vendor", value: vendorId });
                    logRec.setValue({ fieldId: "custrecord_pil_price", value: ln3.price });
                    logRec.setValue({ fieldId: "custrecord_pil_date", value: new Date() });
                    logRec.setValue({ fieldId: "custrecord_pil_po", value: poId });
                    logRec.save();
                } catch (histErr) { log.error("ITEM HISTORY / PRICE LOG ERROR", histErr); }

                // Dọn pool_source + pool_item — guard cho trường hợp đã bị xóa ở lần chạy trước
                try {
                    var srcIdsToDelete = sourceIdsByPoolItem[ln3.poolItemId] || [];
                    for (var d = 0; d < srcIdsToDelete.length; d++) {
                        try { record.delete({ type: "customrecord_pr_pool_source", id: srcIdsToDelete[d] }); }
                        catch (delSrcErr) { log.debug("POOL SOURCE ALREADY GONE", srcIdsToDelete[d]); }
                    }
                    try { record.delete({ type: "customrecord_pr_pool_item", id: ln3.poolItemId }); }
                    catch (delItemErr) { log.debug("POOL ITEM ALREADY GONE", ln3.poolItemId); }
                } catch (delErr) { log.error("DELETE POOL ITEM ERROR", delErr); }
            }

            // Emit cho reduce() gom update PR — 1 entry / dòng PR nguồn
            for (var q = 0; q < prUpdateQueue.length; q++) {
                var item2 = prUpdateQueue[q];
                context.write({ key: item2.prId, value: JSON.stringify(item2) });
            }
        }

        function emitPrUpdatesFromDoneLines(context, lines) {
            // Trường hợp group đã "done" hết từ trước (retry) — không còn prUpdateQueue
            // trong bộ nhớ, nhưng PR update là thao tác idempotent (setSublistValue rồi
            // save lại giá trị y hệt không gây hại) nên có thể bỏ qua re-emit an toàn.
            // Không làm gì thêm ở đây — PR chắc chắn đã được cập nhật ở lần chạy thành công trước đó.
        }

        function markJobLineError(lineId, msg) {
            try {
                record.submitFields({
                    type: "customrecord_pr_po_job_line", id: lineId,
                    values: { custrecord_ppjl_status: "error", custrecord_ppjl_errormsg: String(msg).slice(0, 3000) }
                });
            } catch (e) { log.error("MARK JOB LINE ERROR FAILED", e); }
        }

        // ──────────────────────────── reduce ────────────────────────────
        // context.key = prId, context.values = mảng JSON string các line update
        // (có thể đến từ nhiều vendor/PO khác nhau) — load + save PR ĐÚNG 1 LẦN.
        function reduce(context) {
            var prId = context.key;
            var entries = context.values.map(function (v) { return JSON.parse(v); });
            log.debug("REDUCE START", "PR " + prId + " | entries=" + entries.length + " | " + JSON.stringify(entries));

            try {
                var prToUpdate = record.load({ type: "purchaserequisition", id: prId, isDynamic: false });
                for (var u = 0; u < entries.length; u++) {
                    var lEntry = entries[u];
                    if (lEntry.poId === undefined || lEntry.poId === null) {
                        log.error("REDUCE - MISSING poId", "PR " + prId + " lineIdx=" + lEntry.lineIdx);
                        continue; // hoặc throw, tùy bạn muốn hard-fail hay skip
                    }
                    var lIdx = lEntry.lineIdx;

                    prToUpdate.setSublistValue({ sublistId: "item", fieldId: "custcol_pr_linked_po", line: lIdx, value: lEntry.poId });
                    prToUpdate.setSublistValue({ sublistId: "item", fieldId: "custcol_pr_linked_po_status", line: lIdx, value: "Pending Receipt" });
                    if (lEntry.itemName) {
                        prToUpdate.setSublistValue({ sublistId: "item", fieldId: "custcol_pr_item", line: lIdx, value: lEntry.itemName });
                    }
                    if (lEntry.qty !== undefined && lEntry.qty !== null && !isNaN(lEntry.qty) && lEntry.qty > 0) {
                        prToUpdate.setSublistValue({ sublistId: "item", fieldId: "quantity", line: lIdx, value: lEntry.qty });
                    }
                    if (lEntry.amount !== undefined && lEntry.amount !== null && !isNaN(lEntry.amount)) {
                        prToUpdate.setSublistValue({ sublistId: "item", fieldId: "custcol_pr_actual_amount", line: lIdx, value: lEntry.amount });
                    }
                }
                // beforeSubmit của UE script sẽ tự tính lại custbody_pr_actual_total_amount /
                // custbody_pr_po_progress khi save() này chạy — không cần set tay ở đây.
                var savedId = prToUpdate.save({ ignoreMandatoryFields: true });
                log.audit("REDUCE - PR UPDATED OK", "PR " + prId + " saved id=" + savedId);
            } catch (prUpdErr) {
                log.error("REDUCE - PR UPDATE ERROR", "PR " + prId + " | " + prUpdErr.message);
                throw prUpdErr; // để M/R ghi nhận lỗi cho reduce key này, hiện trong reduceSummary.errors
            }
        }

        // ─────────────────────────── summarize ──────────────────────────
        function summarize(context) {
            var script = runtime.getCurrentScript();
            var jobId = script.getParameter({ name: "custscript_ppj_jobid" });

            var mapErrorCount = 0, reduceErrorCount = 0;
            var errorLogLines = [];
            context.mapSummary.errors.iterator().each(function (key, error) {
                mapErrorCount++;
                errorLogLines.push("MAP [" + key + "]: " + error);
                return true;
            });
            context.reduceSummary.errors.iterator().each(function (key, error) {
                reduceErrorCount++;
                errorLogLines.push("REDUCE [PR " + key + "]: " + error);
                return true;
            });

            // Build file tóm tắt từ job_line đã "done"
            var lines = [];
            search.create({
                type: "customrecord_pr_po_job_line",
                filters: [["custrecord_ppjl_job", "is", jobId], "AND", ["custrecord_ppjl_status", "is", "done"]],
                columns: [
                    "custrecord_ppjl_poid", "custrecord_ppjl_potranid", "custrecord_ppjl_vendorlabel",
                    "custrecord_ppjl_itemname", "custrecord_ppjl_qty", "custrecord_ppjl_price", "custrecord_ppjl_amount"
                ]
            }).run().each(function (r) {
                lines.push({
                    poId: r.getValue("custrecord_ppjl_poid"),
                    poTranId: r.getValue("custrecord_ppjl_potranid"),
                    vendorLabel: r.getValue("custrecord_ppjl_vendorlabel"),
                    itemName: r.getValue("custrecord_ppjl_itemname"),
                    qty: parseFloat(r.getValue("custrecord_ppjl_qty")) || 0,
                    price: parseFloat(r.getValue("custrecord_ppjl_price")) || 0,
                    amount: parseFloat(r.getValue("custrecord_ppjl_amount")) || 0
                });
                return true;
            });

            var byPo = {}; // poId -> { poTranId, vendorLabel, lines: [], total }
            for (var i = 0; i < lines.length; i++) {
                var ln = lines[i];
                if (!byPo[ln.poId]) byPo[ln.poId] = { poTranId: ln.poTranId, vendorLabel: ln.vendorLabel, lines: [], total: 0 };
                byPo[ln.poId].lines.push(ln);
                byPo[ln.poId].total += ln.amount;
            }

            var jobRec = record.load({ type: "customrecord_pr_po_job", id: jobId });
            var sourcePrId = jobRec.getValue({ fieldId: "custrecord_ppj_source_pr" });

            var now2 = new Date();
            var summaryTextLines = [];
            summaryTextLines.push("TOM TAT TAO PURCHASE ORDER");
            summaryTextLines.push("PR goc: #" + sourcePrId);
            summaryTextLines.push("Thoi gian tao: " + now2.toLocaleString());
            summaryTextLines.push("");

            var grandTotal = 0;
            for (var poIdKey in byPo) {
                var entry = byPo[poIdKey];
                summaryTextLines.push("================================================");
                summaryTextLines.push("PO #" + entry.poTranId + " - Vendor: " + entry.vendorLabel);
                summaryTextLines.push("------------------------------------------------");
                for (var sl2 = 0; sl2 < entry.lines.length; sl2++) {
                    var l = entry.lines[sl2];
                    summaryTextLines.push((sl2 + 1) + ". " + l.itemName + " | SL: " + l.qty + " | Don gia: " + formatNumber(l.price) + " | Thanh tien: " + formatNumber(l.amount));
                }
                summaryTextLines.push("------------------------------------------------");
                summaryTextLines.push("Tong PO #" + entry.poTranId + ": " + formatNumber(entry.total) + " VND");
                summaryTextLines.push("");
                grandTotal += entry.total;
            }
            summaryTextLines.push("================================================");
            summaryTextLines.push("TONG CONG TAT CA PO: " + formatNumber(grandTotal) + " VND");
            if (errorLogLines.length > 0) {
                summaryTextLines.push("");
                summaryTextLines.push("*** CO " + errorLogLines.length + " LOI - xem chi tiet trong job record hoac Execution Log ***");
            }

            var summaryFileName = "PO_Summary_PR" + sourcePrId + "_" +
                now2.getFullYear() + ("0" + (now2.getMonth() + 1)).slice(-2) + ("0" + now2.getDate()).slice(-2) + "_" +
                ("0" + now2.getHours()).slice(-2) + ("0" + now2.getMinutes()).slice(-2) + ".txt";

            var summaryFileId = null;
            try {
                var summaryFile = file.create({
                    name: summaryFileName, fileType: file.Type.PLAINTEXT,
                    contents: summaryTextLines.join("\n"), folder: SUMMARY_FOLDER_ID
                });
                summaryFileId = summaryFile.save();
            } catch (fileErr) {
                log.error("SUMMARY FILE CREATE ERROR", fileErr);
            }

            var finalStatus = (mapErrorCount + reduceErrorCount) > 0 ? "error" : "done";
            jobRec.setValue({ fieldId: "custrecord_ppj_status", value: finalStatus });
            jobRec.setValue({ fieldId: "custrecord_ppj_po_count", value: Object.keys(byPo).length });
            jobRec.setValue({ fieldId: "custrecord_ppj_error_count", value: mapErrorCount + reduceErrorCount });
            jobRec.setValue({ fieldId: "custrecord_ppj_grandtotal", value: grandTotal });
            if (summaryFileId) jobRec.setValue({ fieldId: "custrecord_ppj_summary_file", value: summaryFileId });
            if (errorLogLines.length > 0) jobRec.setValue({ fieldId: "custrecord_ppj_error_log", value: errorLogLines.join("\n").slice(0, 100000) });
            jobRec.save();

            // Gửi email cho người tạo job (thay cho việc auto-download trong Suitelet cũ)
            try {
                var creatorId = jobRec.getValue({ fieldId: "custrecord_ppj_created_by" });
                if (creatorId) {
                    var emp = record.load({ type: "employee", id: creatorId });
                    var creatorEmail = emp.getValue({ fieldId: "email" });
                    if (creatorEmail) {
                        var attachments = [];
                        if (summaryFileId) attachments.push(file.load({ id: summaryFileId }));
                        email.send({
                            author: runtime.getCurrentUser().id || creatorId,
                            recipients: creatorEmail,
                            subject: (finalStatus === "done" ? "[Hoàn tất] " : "[Có lỗi] ") + "Tạo PO từ PR #" + sourcePrId,
                            body: "Đã xử lý xong yêu cầu tạo PO.\n" +
                                "Số PO đã tạo: " + Object.keys(byPo).length + "\n" +
                                "Tổng tiền: " + formatNumber(grandTotal) + " VND\n" +
                                (mapErrorCount + reduceErrorCount > 0 ? "Số dòng lỗi: " + (mapErrorCount + reduceErrorCount) + " (xem file đính kèm/Execution Log)\n" : "") +
                                "File tóm tắt đính kèm (nếu có).",
                            attachments: attachments
                        });
                    }
                }
            } catch (mailErr) {
                log.error("SUMMARY EMAIL ERROR", mailErr);
            }

            log.audit("JOB SUMMARIZE DONE", "job=" + jobId + " status=" + finalStatus + " poCount=" + Object.keys(byPo).length + " errors=" + (mapErrorCount + reduceErrorCount));
        }

        return { getInputData: getInputData, map: map, reduce: reduce, summarize: summarize };
    });
