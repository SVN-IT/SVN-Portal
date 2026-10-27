/**
 * @NApiVersion 2.x
 * @NScriptType Suitelet
 *
*/
define(["N/record", "N/redirect", "N/log", "N/https", "N/file", "N/search", "N/url", "N/task", "N/runtime"],
  function (record, redirect, log, https, file, search, url, task, runtime) {

  // ⚠️ Không dùng Number.prototype.toLocaleString() trong SuiteScript —
  // Rhino (engine chạy SuiteScript) có bug với toLocaleString, có thể throw
  // "illegal radix 0" với một số giá trị số. Dùng hàm tự viết bên dưới thay thế.
  function formatNumber(num) {
    num = Math.round(Number(num) || 0);
    var sign = num < 0 ? "-" : "";
    var absStr = Math.abs(num).toString();
    absStr = absStr.replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    return sign + absStr;
  }

  // ⭐ MỚI: escape giá trị để nhét an toàn vào attribute value="..." trong HTML
  function escAttr(str) {
    return String(str == null ? "" : str)
      .replace(/&/g, "&amp;")
      .replace(/"/g, "&quot;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  }

  function onRequest(context) {
    var action = context.request.parameters.action;
    var recId  = context.request.parameters.recId;

    // ─── PRINT EXCEL ────────────────────────────────────────────────
    if (action === "printexcel") {
      try {
        var prRecord = record.load({ type: "purchaserequisition", id: recId });
        // custbody_pr_department giờ là List/Record → dùng getText để lấy tên bộ phận
        var department = prRecord.getText({ fieldId: "custbody_pr_department" });
        var items = [];
        var lineCount = prRecord.getLineCount({ sublistId: "item" });
        for (var i = 0; i < lineCount; i++) {
          items.push({
            custcol_pr_item:         prRecord.getSublistValue({ sublistId: "item", fieldId: "custcol_pr_item",         line: i }),
            custcol_pr_item_purpose: prRecord.getSublistValue({ sublistId: "item", fieldId: "custcol_pr_item_purpose", line: i }),
            unit: prRecord.getSublistText({ sublistId: "item", fieldId: "units", line: i }),
            quantity:                prRecord.getSublistValue({ sublistId: "item", fieldId: "quantity",                line: i }),
            estimate_rate:           prRecord.getSublistValue({ sublistId: "item", fieldId: "estimatedrate",           line: i }),
            estimate_amount:         prRecord.getSublistValue({ sublistId: "item", fieldId: "estimatedamount",         line: i }),
          });
        }
        var payload = { custbody_pr_department: department, items: items };
        var apiResponse = https.post({
          url: "https://58fb-117-6-131-250.ngrok-free.app/api/purchaserequest/generate-excel",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        });
        var responseData = JSON.parse(apiResponse.body);
        var excelFile = file.create({
          name: "PurchaseRequest.xlsx",
          fileType: file.Type.EXCEL,
          contents: responseData.fileBase64,
          encoding: file.Encoding.BASE_64,
        });
        context.response.setHeader({ name: "Content-Type",        value: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
        context.response.setHeader({ name: "Content-Disposition", value: "attachment; filename=PurchaseRequest.xlsx" });
        context.response.writeFile({ file: excelFile, isInline: false });
      } catch (e) {
        log.error("PRINT EXCEL ERROR", e);
        context.response.write("ERROR: " + e.message);
      }
      return;
    }

    // ─── APPROVE / REJECT ───────────────────────────────────────────
    if (action === "approve") {
      record.submitFields({ type: "purchaserequisition", id: recId, values: { custbody_pr_status: "Approved" } });
      redirect.toRecord({ type: "purchaserequisition", id: recId });
      return;
    }
    if (action === "reject") {
      var note = context.request.parameters.note || "";
      record.submitFields({
        type: "purchaserequisition",
        id: recId,
        values: {
          custbody_pr_status: "Rejected",
          custbody_approver_note: note
        }
      });
      redirect.toRecord({ type: "purchaserequisition", id: recId });
      return;
    }

    // ─── ADD TO PO LIST — gom item của PR này vào pool chung ────────
    if (action === "addtopolist") {
      try {
        var prRecordForPool = record.load({ type: "purchaserequisition", id: recId });
        var lc = prRecordForPool.getLineCount({ sublistId: "item" });
        var added = 0, skipped = 0, failed = 0;

        // ⭐ MỚI: search 1 LẦN DUY NHẤT lấy hết pool_source hiện có của PR này,
        // build thành Set các lineidx đã tồn tại (dùng object JS làm set, key = string)
        // → không phụ thuộc filter "is" theo prlineidx của NetSuite nữa, tránh hẳn
        // nghi vấn sai kiểu field / độ trễ index giữa các lần search liên tiếp trong 1 script.
        var existingLineIdxSet = {};
        search.create({
          type: "customrecord_pr_pool_source",
          filters: [
            ["custrecord_pps_pr", "is", recId],
            "AND",
            ["isinactive", "is", "F"]
          ],
          columns: ["custrecord_pps_prlineidx"]
        }).run().each(function (r) {
          var idxVal = r.getValue("custrecord_pps_prlineidx");
          existingLineIdxSet[String(idxVal)] = true;
          log.debug("EXISTING POOL SOURCE FOUND", "prlineidx = " + idxVal + " (raw type: " + (typeof idxVal) + ")");
          return true;
        });

        for (var li = 0; li < lc; li++) {
          try {
            var alreadyLinkedPo = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "custcol_pr_linked_po", line: li });
            var nativeLinked    = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "linked", line: li });
            var nativePoId      = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "poid", line: li });
            if (alreadyLinkedPo) {
              log.debug("LINE SKIPPED - already has linked PO", "line " + li + " | PO id = " + alreadyLinkedPo);
              skipped++; continue;
            }

            var itemNameRaw = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "custcol_pr_item", line: li });
            var itemName = (itemNameRaw || "").trim();
            if (!itemName) {
              log.debug("LINE SKIPPED - empty custcol_pr_item", "line " + li);
              skipped++; continue;
            }

            // ⭐ MỚI: kiểm tra bằng JS object thay vì search lại
            if (existingLineIdxSet[String(li)]) {
              log.debug("LINE SKIPPED - already in pool (JS set check)", "line " + li);
              skipped++; continue;
            }

            var qty         = parseFloat(prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "quantity", line: li })) || 0;
            var unit        = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "units", line: li });
            var desc        = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "description", line: li });
            var rate        = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "estimatedrate", line: li });
            var purpose     = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "custcol_pr_item_purpose", line: li });
            var realItem    = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "item", line: li });
            var persistLine = prRecordForPool.getSublistValue({ sublistId: "item", fieldId: "line", line: li });

            var found = search.create({
              type: "customrecord_pr_pool_item",
              filters: [["name", "is", itemName]],
              columns: ["internalid", "custrecord_ppi_qty"]
            }).run().getRange({ start: 0, end: 1 });

            var poolItemId;
            if (found.length > 0) {
              poolItemId = found[0].getValue("internalid");
              var curQty = parseFloat(found[0].getValue("custrecord_ppi_qty")) || 0;
              var updValues = {
                custrecord_ppi_qty:  curQty + qty,
                custrecord_ppi_desc: desc || ""
              };
              if (rate)     updValues.custrecord_ppi_rate     = rate;
              if (realItem) updValues.custrecord_ppi_realitem = realItem;
              if (unit)     updValues.custrecord_ppi_unit     = unit;
              record.submitFields({ type: "customrecord_pr_pool_item", id: poolItemId, values: updValues });
            } else {
              var newPoolItem = record.create({ type: "customrecord_pr_pool_item" });
              newPoolItem.setValue({ fieldId: "name", value: itemName });
              newPoolItem.setValue({ fieldId: "custrecord_ppi_qty",  value: qty });
              newPoolItem.setValue({ fieldId: "custrecord_ppi_unit", value: unit });
              newPoolItem.setValue({ fieldId: "custrecord_ppi_desc", value: desc });
              newPoolItem.setValue({ fieldId: "custrecord_ppi_rate", value: rate });
              if (realItem) newPoolItem.setValue({ fieldId: "custrecord_ppi_realitem", value: realItem });
              poolItemId = newPoolItem.save();
            }

            var src = record.create({ type: "customrecord_pr_pool_source" });
            src.setValue({ fieldId: "name", value: itemName + " - PR " + recId + " line " + li });
            src.setValue({ fieldId: "custrecord_pps_pool_item", value: poolItemId });
            src.setValue({ fieldId: "custrecord_pps_pr",        value: recId });
            src.setValue({ fieldId: "custrecord_pps_prlineidx", value: li });
            src.setValue({ fieldId: "custrecord_pps_prlinenum", value: persistLine });
            src.setValue({ fieldId: "custrecord_pps_qty",       value: qty });
            src.setValue({ fieldId: "custrecord_pps_rate",      value: rate });
            src.setValue({ fieldId: "custrecord_pps_desc",      value: desc });
            src.setValue({ fieldId: "custrecord_pps_purpose",   value: purpose });
            src.setValue({ fieldId: "custrecord_pps_units",     value: unit });
            src.save();

            // ⭐ MỚI: cập nhật ngay set trong bộ nhớ để phòng trường hợp lc có 2 dòng
            // cùng lineidx (không nên xảy ra nhưng phòng thủ thêm không thừa)
            existingLineIdxSet[String(li)] = true;

            added++;
            log.debug("LINE ADDED OK", "line " + li + " | item: " + itemName);

          } catch (lineErr) {
            failed++;
            log.error("LINE FAILED - line " + li, lineErr);
          }
        }

        var msgHtml =
          '<html><body style="font-family:Arial,sans-serif;padding:24px;">' +
          '<p>Đã thêm <strong>' + added + '</strong> dòng vào danh sách chờ tạo PO' +
          (skipped ? (' (bỏ qua ' + skipped + ' dòng đã có PO hoặc đã add trước đó)') : '') +
          (failed  ? (' <span style="color:#c00">— ' + failed + ' dòng bị lỗi, xem Execution Log để biết chi tiết</span>') : '') +
          '.</p><p>Đang quay lại PR...</p>' +
          '<script>setTimeout(function(){ window.location.href = "' +
            url.resolveRecord({ recordType: "purchaserequisition", recordId: recId, isEditMode: false }) +
          '"; }, 1500);</script></body></html>';

        context.response.setHeader({ name: "Content-Type", value: "text/html" });
        context.response.write(msgHtml);
      } catch (e) {
        log.error("ADD TO PO POOL ERROR", e);
        context.response.write("ERROR: " + e.message);
      }
      return;
    }

    // ─── CREATE PO — hiển thị form chọn vendor (đọc từ PO POOL chung) ──
    if (action === "createpo") {
      // Lấy danh sách vendor thuộc Sigma Vietnam
      var vendorList = []; // [{ id, label }]
      var vendorSearch = search.create({
        type: search.Type.VENDOR,
        filters: [
          // ["subsidiary", "anyof", "9"],
          // "AND",
          ["isinactive", "is", "F"]
        ],
        columns: [
          search.createColumn({ name: "internalid" }),
          search.createColumn({ name: "entityid" }),
          search.createColumn({ name: "companyname" }),
        ]
      });

      vendorSearch.run().each(function (result) {
        var vid      = result.getValue({ name: "internalid" });
        var vendorId = result.getValue({ name: "entityid" });
        var compName = result.getValue({ name: "companyname" });
        vendorList.push({
          id:    vid,
          label: compName ? vendorId + " — " + compName : vendorId
        });
        return true;
      });

      // Build vendor options HTML (dùng chung cho mọi row)
      var vendorOptionsHtml = '<li data-value="" style="padding:7px 12px;cursor:pointer;color:#999;font-size:12px;" onmousedown="pickOpt(this)">-- Select Vendor --</li>';
      for (var v = 0; v < vendorList.length; v++) {
        vendorOptionsHtml += '<li data-value="' + vendorList[v].id + '" style="padding:7px 12px;cursor:pointer;font-size:12px;" onmousedown="pickOpt(this)">' + vendorList[v].label + '</li>';
      }

      // ── Đọc TOÀN BỘ PO Pool (không chỉ của PR hiện tại) ──
      var poolItems = [];
      search.create({
        type: "customrecord_pr_pool_item",
        filters: [],
        columns: [
          search.createColumn({ name: "internalid" }),
          search.createColumn({ name: "name" }),
          search.createColumn({ name: "custrecord_ppi_desc" }),
          search.createColumn({ name: "custrecord_ppi_unit" }),
          search.createColumn({ name: "custrecord_ppi_qty" }),
          search.createColumn({ name: "custrecord_ppi_rate" }),
          search.createColumn({ name: "custrecord_ppi_realitem" }),
        ]
      }).run().each(function (r) {
        poolItems.push({
          id:       r.getValue({ name: "internalid" }),
          name:     r.getValue({ name: "name" }),
          desc:     r.getValue({ name: "custrecord_ppi_desc" }),
          unit:     r.getValue({ name: "custrecord_ppi_unit" }),
          qty:      r.getValue({ name: "custrecord_ppi_qty" }),
          rate:     r.getValue({ name: "custrecord_ppi_rate" }),
          realItem: r.getValue({ name: "custrecord_ppi_realitem" }),
        });
        return true;
      });

      // ── Tra PR nguồn + Department cho từng pool item (hiển thị cột "PR: Department") ──
      var sourcesByPoolItem = {}; // poolItemId -> [prId, ...] (unique)
      var allPrIdsSet = {};
      search.create({
        type: "customrecord_pr_pool_source",
        filters: [],
        columns: ["custrecord_pps_pool_item", "custrecord_pps_pr"]
      }).run().each(function (r) {
        var piId    = r.getValue("custrecord_pps_pool_item");
        var prIdVal = r.getValue("custrecord_pps_pr");
        if (!piId || !prIdVal) return true;
        if (!sourcesByPoolItem[piId]) sourcesByPoolItem[piId] = [];
        if (sourcesByPoolItem[piId].indexOf(prIdVal) === -1) sourcesByPoolItem[piId].push(prIdVal);
        allPrIdsSet[prIdVal] = true;
        return true;
      });

      var prInfoMap = {}; // prId -> { tranid, deptText }
      var allPrIds = Object.keys(allPrIdsSet);
      if (allPrIds.length > 0) {
        search.create({
          type: "purchaserequisition",
          filters: [["internalid", "anyof", allPrIds]],
          columns: ["internalid", "tranid", "custbody_pr_department"]
        }).run().each(function (r) {
          prInfoMap[r.getValue("internalid")] = {
            tranid:   r.getValue("tranid"),
            deptText: r.getText("custbody_pr_department") || ""
          };
          return true;
        });
      }

      if (poolItems.length === 0) {
        var emptyHtml =
          '<html><body style="font-family:Arial,sans-serif;padding:24px;">' +
          '<p>Danh sách chờ tạo PO đang trống.</p>' +
          '<p>Vui lòng bấm <strong>"Add to PO list"</strong> ở các PR đã Approved trước, sau đó quay lại bấm Create PO.</p>' +
          '<button onclick="history.back()" style="padding:8px 16px;">Quay lại</button>' +
          '</body></html>';
        context.response.write(emptyHtml);
        return;
      }

      // ⭐ TỐI ƯU (Fix #1): tra last vendor/price cho TẤT CẢ pool item bằng 1 lần search
      // duy nhất (thay vì search riêng từng item trong loop bên dưới — với N item cũ
      // là N search tuần tự, rất chậm khi N lớn). "name" trên customrecord_pr_item_history
      // là Free-Form Text nên không dùng được "anyof" — build filter OR theo từng tên,
      // chia batch 100 tên/lần để tránh filter quá dài.
      var itemHistoryMap = {}; // trimmedName -> { vendorId, price }
      (function loadItemHistoryBatch() {
        var names = [];
        var seen = {};
        for (var ni = 0; ni < poolItems.length; ni++) {
          var nm = (poolItems[ni].name || "").trim();
          if (nm && !seen[nm]) { seen[nm] = true; names.push(nm); }
        }
        var BATCH_SIZE = 100;
        for (var bi = 0; bi < names.length; bi += BATCH_SIZE) {
          var batch = names.slice(bi, bi + BATCH_SIZE);
          var orFilters = [];
          for (var fi = 0; fi < batch.length; fi++) {
            if (orFilters.length > 0) orFilters.push("or");
            orFilters.push(["name", "is", batch[fi]]);
          }
          search.create({
            type: "customrecord_pr_item_history",
            filters: orFilters,
            columns: ["name", "custrecord_pr_item_last_vendor", "custrecord_pr_item_last_purchase_price"]
          }).run().each(function (r) {
            var key = (r.getValue("name") || "").trim();
            if (key && !itemHistoryMap[key]) {
              itemHistoryMap[key] = {
                vendorId: r.getValue("custrecord_pr_item_last_vendor"),
                price:    r.getValue("custrecord_pr_item_last_purchase_price")
              };
            }
            return true;
          });
        }
      })();

      // ⭐ TỐI ƯU: build map vendorId -> label 1 lần, tránh loop vendorList (mảng có thể dài)
      // lặp lại cho mỗi pool item bên dưới.
      var vendorLabelById = {};
      for (var vb = 0; vb < vendorList.length; vb++) {
        vendorLabelById[String(vendorList[vb].id)] = vendorList[vb].label;
      }

      // Build bảng item rows từ pool
      var itemRows = "";
      for (var i = 0; i < poolItems.length; i++) {
        var p = poolItems[i];
        var qty    = parseFloat(p.qty) || 0;
        var rate   = parseFloat(p.rate) || 0;
        var amount = qty * rate;

        // ── Tra last vendor từ item history (đọc từ map đã batch-load ở trên) ──
        var lastVendorId    = "";
        var lastVendorLabel = "-- Select Vendor --";
        var lastPrice       = rate || 0;

        var histEntry = p.name ? itemHistoryMap[p.name.trim()] : null;
        if (histEntry) {
          if (histEntry.vendorId) {
            lastVendorId = histEntry.vendorId;
            lastVendorLabel = vendorLabelById[String(histEntry.vendorId)] || lastVendorLabel;
          }
          if (histEntry.price) lastPrice = histEntry.price;
        }

        var dispColor = lastVendorId ? "#333" : "#999";

        // ⭐ MỚI: escape tên item để nhét an toàn vào value="..."
        var safeName = escAttr(p.name);
        var deptCellHtml = "";
        var srcPrIds = sourcesByPoolItem[p.id] || [];
        if (srcPrIds.length > 0) {
          var deptParts = [];
          for (var dp = 0; dp < srcPrIds.length; dp++) {
            var info = prInfoMap[srcPrIds[dp]];
            if (info) {
              deptParts.push("PR#" + info.tranid + (info.deptText ? " (" + info.deptText + ")" : ""));
            }
          }
          deptCellHtml = deptParts.join("<br>");
        }
        itemRows +=
          '<tr id="row_' + i + '">' +
            '<td style="padding:8px;border:1px solid #ddd;text-align:center;">' +
              '<input type="checkbox" name="select_' + i + '" class="row-select-cb" checked>' +
            '</td>' +
            '<td style="padding:8px;border:1px solid #ddd;text-align:center;">' + (i + 1) + '</td>' +

            // ⭐ MỚI: Item name giờ là input text, cho sửa được. origname_i giữ tên gốc để tham chiếu.
            '<td style="padding:8px;border:1px solid #ddd;">' +
              '<input type="text" name="itemname_' + i + '" value="' + safeName + '" ' +
              'style="width:100%;min-width:160px;box-sizing:border-box;padding:5px 7px;border:1px solid #ccc;border-radius:4px;" />' +
              '<input type="hidden" name="origname_' + i + '" value="' + safeName + '">' +
            '</td>' +
            '<td style="padding:8px;border:1px solid #ddd;font-size:12px;color:#555;">' + deptCellHtml + '</td>' +

            '<td style="padding:8px;border:1px solid #ddd;">' + (p.desc || "") + '</td>' +
            '<td style="padding:8px;border:1px solid #ddd;text-align:center;">' + (p.unit || "") + '</td>' +

            // ⭐ MỚI: Qty giờ là input number, cho sửa được. origqty_i giữ số lượng gốc để
            // server tính tỉ lệ chia lại cho từng dòng PR nguồn.
            '<td style="padding:8px;border:1px solid #ddd;text-align:center;">' +
              '<input type="number" step="any" min="0" name="qty_' + i + '" value="' + qty + '" ' +
              'oninput="updateAmount(' + i + ')" ' +
              'style="width:80px;padding:5px 7px;border:1px solid #ccc;border-radius:4px;text-align:center;" />' +
              '<input type="hidden" name="origqty_' + i + '" value="' + qty + '">' +
            '</td>' +

            '<td style="padding:8px;border:1px solid #ddd;text-align:right;">' +
              formatNumber(rate) +
              '<input type="hidden" name="estrate_' + i + '" value="' + rate + '">' +
            '</td>' +

            // ⭐ MỚI: bọc amount trong span để JS cập nhật lại khi Qty đổi
            '<td style="padding:8px;border:1px solid #ddd;text-align:right;">' +
              '<span id="amount_disp_' + i + '">' + formatNumber(amount) + '</span>' +
            '</td>' +

            // ── CỘT UNIT PRICE (áp dụng chung cho mọi dòng nguồn của item này) ──
            '<td style="padding:8px;border:1px solid #ddd;text-align:right;">' +
              '<input type="number" name="price_' + i + '" value="' + lastPrice + '" ' +
              'style="width:110px;padding:4px 6px;border:1px solid #ccc;border-radius:4px;text-align:right;" />' +
            '</td>' +

            // ── CỘT VENDOR DROPDOWN ──
            '<td style="padding:8px;border:1px solid #ddd;">' +
              '<div class="csw" style="position:relative;">' +
                '<input type="hidden" name="vendor_' + i + '" class="vendor-value" value="' + lastVendorId + '">' +
                '<div class="sel-display" tabindex="0" onclick="toggleDD(this)" style="border:1px solid #ccc;border-radius:4px;padding:6px 28px 6px 8px;cursor:pointer;background:#fff;position:relative;min-width:180px;">' +
                  '<span class="disp-text" style="color:' + dispColor + ';">' + lastVendorLabel + '</span>' +
                  '<span style="position:absolute;right:8px;top:50%;transform:translateY(-50%);pointer-events:none;font-size:11px;">▾</span>' +
                '</div>' +
                '<div class="dd-panel" style="display:none;position:absolute;z-index:9999;top:100%;left:0;width:280px;background:#fff;border:1px solid #ccc;border-radius:4px;box-shadow:0 4px 12px rgba(0,0,0,.15);">' +
                  '<div style="padding:6px 6px 4px;">' +
                    '<input type="text" placeholder="🔍 Search..." oninput="filterDD(this)" style="width:100%;box-sizing:border-box;padding:5px 8px;border:1px solid #ddd;border-radius:4px;font-size:12px;">' +
                  '</div>' +
                  '<ul class="opt-list" style="list-style:none;margin:0;padding:0 0 4px;max-height:220px;overflow-y:auto;">' +
                    vendorOptionsHtml +
                  '</ul>' +
                '</div>' +
              '</div>' +
              '<input type="hidden" name="lineindex_' + i + '" value="' + i + '">' +
              // ⭐ id của pool item — dùng để submitpo biết dòng này ứng với pool item nào
              '<input type="hidden" name="poolitem_' + i + '" value="' + p.id + '">' +
            '</td>' +
            '<td style="padding:8px;border:1px solid #ddd;text-align:center;">' +
              '<button type="button" onclick="removeFromPool(\'' + p.id + '\', ' + i + ')" ' +
              'style="background:#dc2626;color:#fff;border:none;padding:6px 10px;border-radius:4px;cursor:pointer;font-size:12px;">Xóa</button>' +
            '</td>' +
          '</tr>';
      }

      var submitUrl = url.resolveScript({
        scriptId:     "customscriptsvn_pr_approval_sl",
        deploymentId: "customdeploysvn_pr_approval_sl",
        params: { action: "submitpo", recId: recId }
      });
      var saveUrl = url.resolveScript({
        scriptId:     "customscriptsvn_pr_approval_sl",
        deploymentId: "customdeploysvn_pr_approval_sl",
        params: { action: "savepool", recId: recId }
      });
      var removeUrlBase = url.resolveScript({
        scriptId:     "customscriptsvn_pr_approval_sl",
        deploymentId: "customdeploysvn_pr_approval_sl",
        params: { action: "removefrompool" }
      });
      var html =
        '<!DOCTYPE html><html><head>' +
        '<meta charset="UTF-8">' +
        '<title>Create Purchase Orders — PO Pool</title>' +
        '<style>' +
          'body { font-family: Arial, sans-serif; font-size: 13px; padding: 24px; color: #333; }' +
          'h2 { margin-bottom: 4px; }' +
          'p.sub { color: #666; margin-bottom: 20px; }' +
          'table { border-collapse: collapse; width: 100%; }' +
          'th { background: #f5f5f5; padding: 8px 10px; border: 1px solid #ddd; text-align: left; }' +
          '.btn { background: #1a73e8; color: #fff; border: none; padding: 10px 24px; border-radius: 4px; font-size: 14px; cursor: pointer; margin-top: 20px; }' +
          '.btn:hover { background: #1558b0; }' +
          '.btn-cancel { background: #888; margin-left: 12px; }' +
          '.loading-overlay { display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); z-index: 9999; justify-content: center; align-items: center; flex-direction: column; }' +
          '.spinner { border: 4px solid #f3f3f3; border-top: 4px solid #1a73e8; border-radius: 50%; width: 50px; height: 50px; animation: spin 1s linear infinite; }' +
          '@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }' +
        '</style>' +
        '</head><body>' +
        '<h2>Create Purchase Orders</h2>' +
        (function () {
          var savedQ = context.request.parameters.saved;
          var errQ   = context.request.parameters.saveerr;
          if (savedQ === undefined || savedQ === null) return "";
          var errNum = parseInt(errQ, 10) || 0;
          var color = errNum > 0 ? "#b45309" : "#0f9d58";
          var bg    = errNum > 0 ? "#fff7ed" : "#e6f4ea";
          return '<div style="background:' + bg + ';color:' + color + ';border-radius:6px;padding:10px 14px;margin-bottom:14px;font-size:13px;">' +
            'Đã lưu ' + savedQ + ' dòng.' + (errNum > 0 ? ' Có ' + errNum + ' dòng lưu lỗi — xem log để kiểm tra.' : '') +
            '</div>';
        })() +
        '<p class="sub">Danh sách chờ tạo PO (gộp từ nhiều PR) — Assign a vendor to each line item. Lines with the same vendor will be grouped into one PO. Same item from different PR sẽ nằm chung 1 PO nhưng tách thành các dòng riêng (giữ đúng Linked Order cho từng PR). ' +
          '<strong>Có thể sửa Item Name và Qty trực tiếp trên bảng</strong> — nếu Qty bị sửa, hệ thống sẽ tự chia tỉ lệ lại cho từng PR nguồn; thay đổi sẽ được cập nhật ngược lại đúng dòng PR gốc và dòng PO tạo ra. ' +
          '<strong>Bấm "Lưu"</strong> để lưu lại tên/số lượng/đơn giá/vendor đã sửa cho CÁC DÒNG ĐANG TÍCH ✓ (chưa tạo PO) — dòng nào bỏ tích sẽ KHÔNG được lưu. Nếu bảng nhiều dòng, nên bấm "Deselect All" rồi tích từng nhóm nhỏ (~15-20 dòng) và Lưu theo từng đợt để tránh vượt giới hạn xử lý. Bấm "Create POs" khi đã sẵn sàng tạo PO thật (cũng chỉ áp dụng cho dòng đang tích).</p>' +
        '<form method="POST" action="' + submitUrl + '">' +
          '<input type="hidden" name="linecount" value="' + poolItems.length + '">' +
          '<div style="margin-bottom:10px;">' +
            '<button type="button" class="btn-select" onclick="toggleAllRows(true)" ' +
              'style="background:#f1f3f4;color:#333;border:1px solid #ccc;padding:6px 14px;border-radius:4px;cursor:pointer;font-size:12px;margin-right:8px;">Select All</button>' +
            '<button type="button" class="btn-select" onclick="toggleAllRows(false)" ' +
              'style="background:#f1f3f4;color:#333;border:1px solid #ccc;padding:6px 14px;border-radius:4px;cursor:pointer;font-size:12px;">Deselect All</button>' +
          '</div>' +
          '<table>' +
            '<thead><tr>' +
              '<th style="width:36px;text-align:center;">✓</th>' +
              '<th>#</th>' +
              '<th style="min-width:160px;">Item</th>' +
              '<th style="min-width:150px;">PR: Department</th>' +
              '<th>Description</th>' +
              '<th>Unit</th>' +
              '<th>Qty</th>' +
              '<th>Est. Rate</th>' +
              '<th>Est. Amount</th>' +
              '<th style="min-width:130px;">Unit Price</th>' +
              '<th style="min-width:200px;">Vendor</th>' +
              '<th style="width:70px;text-align:center;">Xóa</th>' +
            '</tr></thead>' +
            '<tbody>' + itemRows + '</tbody>' +
          '</table>' +
          '<button type="submit" class="btn" onclick="showLoading(event)">Create POs</button>' +
          '<button type="submit" class="btn btn-save" formaction="' + saveUrl + '" onclick="showLoading(event,\'Đang lưu thay đổi...\')" ' +
            'style="background:#0f9d58;">Lưu</button>' +
          '<button type="button" class="btn btn-cancel" onclick="history.back()">Cancel</button>' +
        '</form>' +
        '<div class="loading-overlay" id="loadingOverlay">' +
          '<div class="spinner"></div>' +
          '<div id="loadingText" style="margin-top:14px;color:#333;font-size:14px;font-family:Arial,sans-serif;"></div>' +
        '</div>' +
        '<script>' +
          'function showLoading(e, msg){' +
          '  document.getElementById("loadingText").textContent = msg || "Đang tạo Purchase Order...";' +
          '  document.getElementById("loadingOverlay").style.display="flex";' +
          '}' +
          // ⭐ MỚI: Select All / Deselect All — chỉ toggle checkbox của các dòng
          // ĐANG hiển thị trên trang (dòng đã bị xóa bằng nút "Xóa" thì element
          // không còn trong DOM nên tự động không bị ảnh hưởng).
          'function toggleAllRows(checked){' +
          '  document.querySelectorAll(".row-select-cb").forEach(function(cb){ cb.checked = checked; });' +
          '}' +
          // ⭐ MỚI: cập nhật hiển thị Est. Amount khi user sửa Qty (amount = qty * est.rate gốc,
          // chỉ để tham khảo trên UI — số liệu thật tính lại chính xác ở server khi submit)
          'function updateAmount(i){' +
          '  var qtyEl = document.getElementsByName("qty_" + i)[0];' +
          '  var rateEl = document.getElementsByName("estrate_" + i)[0];' +
          '  var disp = document.getElementById("amount_disp_" + i);' +
          '  if(!qtyEl || !rateEl || !disp) return;' +
          '  var q = parseFloat(qtyEl.value) || 0;' +
          '  var r = parseFloat(rateEl.value) || 0;' +
          '  var val = Math.round(q * r).toString().replace(/\\B(?=(\\d{3})+(?!\\d))/g, ",");' +
          '  disp.textContent = val;' +
          '}' +
          'function toggleDD(el){' +
          '  var panel=el.nextElementSibling;' +
          '  var isOpen=panel.style.display==="block";' +
          '  document.querySelectorAll(".dd-panel").forEach(function(p){p.style.display="none";});' +
          '  if(!isOpen){panel.style.display="block";panel.querySelector("input").focus();}' +
          '}' +
          'function filterDD(input){' +
          '  var kw=input.value.toLowerCase();' +
          '  var list=input.closest(".dd-panel").querySelector(".opt-list");' +
          '  list.querySelectorAll("li").forEach(function(li){' +
          '    li.style.display=li.textContent.toLowerCase().includes(kw)?"":"none";' +
          '  });' +
          '}' +
          'function pickOpt(li){' +
          '  var wrapper=li.closest(".csw");' +
          '  wrapper.querySelector(".vendor-value").value=li.dataset.value;' +
          '  wrapper.querySelector(".disp-text").textContent=li.textContent;' +
          '  wrapper.querySelector(".disp-text").style.color=li.dataset.value?"#333":"#999";' +
          '  wrapper.querySelector(".dd-panel").style.display="none";' +
          '  wrapper.querySelector("input[type=text]").value="";' +
          '  filterDD(wrapper.querySelector("input[type=text]"));' +
          '}' +
          'function removeFromPool(poolItemId, idx){' +
          '  if(!confirm("Xóa item này khỏi danh sách chờ tạo PO? (Không ảnh hưởng PR gốc)")) return;' +
          '  fetch("' + removeUrlBase + '&poolItemId=" + poolItemId)' +
          '    .then(function(r){ return r.json(); })' +
          '    .then(function(res){' +
          '      if(res.success){' +
          '        var row = document.getElementById("row_" + idx);' +
          '        if(row) row.remove();' +
          '      } else {' +
          '        alert("Lỗi: " + (res.message || "Không xóa được"));' +
          '      }' +
          '    })' +
          '    .catch(function(){ alert("Lỗi kết nối tới server"); });' +
          '}' +
          'document.addEventListener("click",function(e){' +
          '  if(!e.target.closest(".csw")){' +
          '    document.querySelectorAll(".dd-panel").forEach(function(p){p.style.display="none";});' +
          '  }' +
          '});' +
        '</script>' +
        '</body></html>';

      context.response.setHeader({ name: "Content-Type", value: "text/html" });
      context.response.write(html);
      return;
    }

    /**
 * PATCH cho svn_pr_approval_sl.js
 * ─────────────────────────────────────────────────────────────────
 * 1. Thêm "N/task" vào define([...]) đầu file, ví dụ:
 *
 *    define(["N/record", "N/redirect", "N/log", "N/https", "N/file", "N/search", "N/url", "N/task", "N/runtime"],
 *    function (record, redirect, log, https, file, search, url, task, runtime) {
 *
 * 2. XÓA toàn bộ khối `if (action === "submitpo") { ... }` hiện tại trong onRequest
 *    (đoạn tạo PO đồng bộ) và THAY bằng 2 khối bên dưới (submitpo bản mới + pojobstatus mới).
 *
 * 3. Sửa 2 hằng số MR_SCRIPT_ID / MR_DEPLOYMENT_ID cho khớp với script/deployment
 *    Map/Reduce thật sự bạn tạo (xem 00_SETUP_TRUOC_KHI_DEPLOY.md).
 * ─────────────────────────────────────────────────────────────────
 */

var MR_SCRIPT_ID = "customscript_svn_pr_po_mr";       // ⚠️ đổi cho khớp script id thật
var MR_DEPLOYMENT_ID = "customdeploy_svn_pr_po_mr";   // ⚠️ đổi cho khớp deployment id thật (hoặc bỏ trống để dùng deployment mặc định)

// ─── SUBMIT PO (bản mới) — chỉ tạo job + trigger Map/Reduce, KHÔNG tạo PO ngay ──
if (action === "submitpo") {
  try {
    var lineCount = parseInt(context.request.parameters.linecount);

    // Đọc TOÀN BỘ dòng đang tích, y hệt logic cũ khi build vendorMap —
    // nhưng giờ chỉ để tạo job_line, không xử lý gì thêm ở đây.
    var rowsForJob = [];
    var missingVendorCount = 0;
    for (var i = 0; i < lineCount; i++) {
      var isSelected = !!context.request.parameters["select_" + i];
      if (!isSelected) continue;

      var vendorId = context.request.parameters["vendor_" + i];
      var poolItemId = context.request.parameters["poolitem_" + i];
      var price = parseFloat(context.request.parameters["price_" + i]) || 0;
      var editedName = (context.request.parameters["itemname_" + i] || "").trim();
      var origName = (context.request.parameters["origname_" + i] || "").trim();
      var editedQty = parseFloat(context.request.parameters["qty_" + i]);
      var origQty = parseFloat(context.request.parameters["origqty_" + i]) || 0;

      if (!editedName) editedName = origName;
      if (isNaN(editedQty) || editedQty <= 0) editedQty = origQty;

      if (!vendorId) { missingVendorCount++; continue; } // giữ nguyên hành vi cũ: dòng không chọn vendor thì bỏ qua

      rowsForJob.push({
        poolItemId: poolItemId, vendorId: vendorId, price: price,
        itemName: editedName, qty: editedQty, origQty: origQty
      });
    }

    if (rowsForJob.length === 0) {
      context.response.write(
        "<html><body style='font-family:Arial,sans-serif;padding:24px;'>" +
        "<p>Không có dòng nào hợp lệ để tạo PO (kiểm tra lại đã tích chọn và chọn Vendor chưa).</p>" +
        "<button onclick='history.back()'>Quay lại</button></body></html>"
      );
      return;
    }

    // Tạo job header
    var jobRecord = record.create({ type: "customrecord_pr_po_job" });
    jobRecord.setValue({ fieldId: "name", value: "PO Job - PR " + recId + " - " + (new Date()).toLocaleString() });
    jobRecord.setValue({ fieldId: "custrecord_ppj_source_pr", value: recId });
    jobRecord.setValue({ fieldId: "custrecord_ppj_status", value: "pending" });
    jobRecord.setValue({ fieldId: "custrecord_ppj_created_by", value: runtime.getCurrentUser().id });
    var jobId = jobRecord.save();

    // Tạo job_line cho từng dòng đã chọn
    for (var r2 = 0; r2 < rowsForJob.length; r2++) {
      var row = rowsForJob[r2];
      var jl = record.create({ type: "customrecord_pr_po_job_line" });
      jl.setValue({ fieldId: "name", value: "Job " + jobId + " - Line " + r2 });
      jl.setValue({ fieldId: "custrecord_ppjl_job", value: jobId });
      jl.setValue({ fieldId: "custrecord_ppjl_poolitem", value: row.poolItemId });
      jl.setValue({ fieldId: "custrecord_ppjl_vendor", value: row.vendorId });
      jl.setValue({ fieldId: "custrecord_ppjl_price", value: row.price });
      jl.setValue({ fieldId: "custrecord_ppjl_itemname", value: row.itemName });
      jl.setValue({ fieldId: "custrecord_ppjl_qty", value: row.qty });
      jl.setValue({ fieldId: "custrecord_ppjl_origqty", value: row.origQty });
      jl.setValue({ fieldId: "custrecord_ppjl_status", value: "pending" });
      jl.save();
    }

    // Trigger Map/Reduce
    try {
      var mrTask = task.create({ taskType: task.TaskType.MAP_REDUCE });
      mrTask.scriptId = MR_SCRIPT_ID;
      if (MR_DEPLOYMENT_ID) mrTask.deploymentId = MR_DEPLOYMENT_ID;
      mrTask.params = { custscript_ppj_jobid: jobId };
      mrTask.submit();
      jobRecord.setValue({ fieldId: "custrecord_ppj_status", value: "processing" });
      jobRecord.save();
    } catch (taskErr) {
      log.error("TRIGGER MAP/REDUCE ERROR", taskErr);
      jobRecord.setValue({ fieldId: "custrecord_ppj_status", value: "error" });
      jobRecord.setValue({ fieldId: "custrecord_ppj_error_log", value: "Không trigger được Map/Reduce: " + taskErr.message });
      jobRecord.save();
    }

    // Redirect sang trang trạng thái (poll tự động)
    var statusUrl = url.resolveScript({
      scriptId: "customscriptsvn_pr_approval_sl",
      deploymentId: "customdeploysvn_pr_approval_sl",
      params: { action: "pojobstatus", jobId: jobId, recId: recId }
    });
    redirect.redirect({ url: statusUrl });
    return;
  } catch (e) {
    log.error("SUBMIT PO (CREATE JOB) ERROR", e);
    context.response.write("ERROR: " + e.message);
  }
  return;
}

// ─── POJOBSTATUS — trang trạng thái, tự refresh cho tới khi job xong ──
if (action === "pojobstatus") {
  try {
    var jobIdQ = context.request.parameters.jobId;
    var recIdQ = context.request.parameters.recId;

    var jobRec = record.load({ type: "customrecord_pr_po_job", id: jobIdQ });
    var status = jobRec.getValue({ fieldId: "custrecord_ppj_status" });
    var poCount = jobRec.getValue({ fieldId: "custrecord_ppj_po_count" }) || 0;
    var errorCount = jobRec.getValue({ fieldId: "custrecord_ppj_error_count" }) || 0;
    var grandTotal = jobRec.getValue({ fieldId: "custrecord_ppj_grandtotal" }) || 0;
    var fileId = jobRec.getValue({ fieldId: "custrecord_ppj_summary_file" });

    var prUrl = url.resolveRecord({ recordType: "purchaserequisition", recordId: recIdQ, isEditMode: false });

    var bodyHtml;
    if (status === "pending" || status === "processing") {
      var selfUrl = url.resolveScript({
        scriptId: "customscriptsvn_pr_approval_sl",
        deploymentId: "customdeploysvn_pr_approval_sl",
        params: { action: "pojobstatus", jobId: jobIdQ, recId: recIdQ }
      });
      bodyHtml =
        '<div style="text-align:center;padding:60px 20px;">' +
          '<div style="border:4px solid #f3f3f3;border-top:4px solid #1a73e8;border-radius:50%;width:50px;height:50px;' +
          'animation:spin 1s linear infinite;margin:0 auto 20px;"></div>' +
          '<p style="font-size:15px;color:#333;">Đang tạo Purchase Order, vui lòng chờ...</p>' +
          '<p style="font-size:12px;color:#888;">Trang sẽ tự cập nhật, không cần bấm gì thêm.</p>' +
        '</div>' +
        '<style>@keyframes spin{0%{transform:rotate(0deg);}100%{transform:rotate(360deg);}}</style>' +
        '<script>setTimeout(function(){ window.location.href = "' + selfUrl + '"; }, 4000);</script>';
    } else if (status === "done") {
      // ⭐ Auto-download file .txt ngay khi job xong — giống hành vi bản đồng bộ cũ,
      // thay vì bắt user bấm link thủ công. Nhúng thẳng nội dung file vào HTML rồi
      // tạo Blob + click ảo (không dùng f.url trực tiếp vì browser có thể mở file
      // .txt inline thay vì tải về).
      var downloadScript = "";
      var manualLinkHtml = "";
      if (fileId) {
        try {
          var f = file.load({ id: fileId });
          var fileContents = f.getContents();
          var fileNameForDownload = f.name;
          downloadScript =
            '<script>' +
            '  var summaryContent = ' + JSON.stringify(fileContents) + ';' +
            '  var fileName = ' + JSON.stringify(fileNameForDownload) + ';' +
            '  function triggerDownload(){' +
            '    var blob = new Blob([summaryContent], {type:"text/plain;charset=utf-8"});' +
            '    var blobUrl = URL.createObjectURL(blob);' +
            '    var a = document.createElement("a");' +
            '    a.href = blobUrl; a.download = fileName;' +
            '    document.body.appendChild(a); a.click(); document.body.removeChild(a);' +
            '  }' +
            '  triggerDownload();' +
            '  document.getElementById("manualDownload").onclick = function(e){ e.preventDefault(); triggerDownload(); };' +
            '</script>';
          manualLinkHtml = '<p><a id="manualDownload" href="#">Nếu file không tự tải, bấm vào đây</a></p>';
        } catch (fErr) {
          log.error("LOAD SUMMARY FILE ERROR", fErr);
          manualLinkHtml = '<p style="color:#b45309;">Không đọc được file tóm tắt, vui lòng kiểm tra trong job record.</p>';
        }
      }
      bodyHtml =
        '<div style="text-align:center;padding:60px 20px;">' +
          '<div style="width:50px;height:50px;border-radius:50%;background:#0f9d58;color:#fff;' +
          'display:flex;align-items:center;justify-content:center;font-size:28px;margin:0 auto 20px;">✓</div>' +
          '<p style="font-size:15px;color:#333;">Đã tạo thành công <strong>' + poCount + '</strong> Purchase Order.</p>' +
          '<p style="font-size:14px;color:#333;">Tổng tiền: <strong>' + formatNumber(grandTotal) + ' VND</strong></p>' +
          '<p style="font-size:13px;color:#888;">File tóm tắt đang được tải xuống...</p>' +
          manualLinkHtml +
          '<p><a href="' + prUrl + '">Quay lại PR</a></p>' +
        '</div>' + downloadScript;
    } else {
      // status === "error"
      var errorLog = jobRec.getValue({ fieldId: "custrecord_ppj_error_log" }) || "";
      bodyHtml =
        '<div style="padding:24px;">' +
          '<p>⚠️ Có <strong>' + errorCount + '</strong> lỗi khi tạo PO' +
          (poCount ? (' (đã tạo được ' + poCount + ' PO cho các vendor không lỗi)') : '') + '.</p>' +
          '<p>Vui lòng kiểm tra Execution Log hoặc liên hệ IT. Chi tiết lỗi:</p>' +
          '<pre style="background:#f5f5f5;padding:12px;border-radius:6px;font-size:12px;white-space:pre-wrap;">' +
          errorLog.replace(/</g, "&lt;") + '</pre>' +
          '<p><a href="' + prUrl + '">Quay lại PR</a></p>' +
        '</div>';
    }

    context.response.setHeader({ name: "Content-Type", value: "text/html" });
    context.response.write(
      '<!DOCTYPE html><html><head><meta charset="UTF-8"><title>Trạng thái tạo PO</title></head>' +
      '<body style="font-family:Arial,sans-serif;color:#333;">' + bodyHtml + '</body></html>'
    );
  } catch (e) {
    log.error("POJOBSTATUS ERROR", e);
    context.response.write("ERROR: " + e.message);
  }
  return;
}

    // ─── SAVE POOL EDITS (nút "Lưu" — chỉ lưu name/qty/price/vendor đã sửa, KHÔNG
    // tạo PO, KHÔNG xóa pool, KHÔNG ghi custcol_pr_linked_po vì chưa có PO nào cả) ───
    if (action === "savepool") {
      try {
        var lineCountS = parseInt(context.request.parameters.linecount) || 0;
        var savedCountS = 0, errorCountS = 0;

        // ── BƯỚC 1: đọc TOÀN BỘ input từ request trước (không tốn API call — chỉ đọc
        // params trong bộ nhớ), gom vào mảng rowsToSave để xử lý theo batch bên dưới.
        // CHỈ lưu những dòng đang được TÍCH checkbox (select_i) — giống hành vi của
        // "Create POs" — để "Deselect All + tích từng nhóm nhỏ" thực sự dùng được làm
        // cách lưu theo đợt, tránh vượt giới hạn Usage Limit khi bảng có nhiều dòng. ──
        var rowsToSave = [];
        for (var si2 = 0; si2 < lineCountS; si2++) {
          var poolItemIdS = context.request.parameters["poolitem_" + si2];
          if (!poolItemIdS) continue; // dòng đã bị xóa khỏi trang từ trước → bỏ qua

          var isSelectedS = !!context.request.parameters["select_" + si2];
          if (!isSelectedS) continue; // không tích → bỏ qua, không lưu dòng này

          var editedNameS = (context.request.parameters["itemname_" + si2] || "").trim();
          var origNameS   = (context.request.parameters["origname_"  + si2] || "").trim();
          var editedQtyS  = parseFloat(context.request.parameters["qty_" + si2]);
          var origQtyS    = parseFloat(context.request.parameters["origqty_" + si2]) || 0;
          var priceS      = parseFloat(context.request.parameters["price_" + si2]);
          var vendorIdS   = context.request.parameters["vendor_" + si2];

          if (!editedNameS) editedNameS = origNameS;
          if (isNaN(editedQtyS) || editedQtyS <= 0) editedQtyS = origQtyS;

          rowsToSave.push({
            idx:        si2,
            poolItemId: poolItemIdS,
            name:       editedNameS,
            qty:        editedQtyS,
            origQty:    origQtyS,
            price:      priceS,
            vendorId:   vendorIdS,
            qtyChanged: origQtyS > 0 && Math.abs(editedQtyS - origQtyS) > 0.0001
          });
        }

        // ── BƯỚC 2 — TỐI ƯU: search TOÀN BỘ pool_source của TẤT CẢ pool item trong
        // 1 lần (dùng "anyof" vì custrecord_pps_pool_item là List/Record), thay vì search
        // riêng từng dòng như bản trước — đây là nguyên nhân chính gây "Usage Limit Exceeded"
        // khi lưu nhiều dòng cùng lúc. Chia batch 100 id/lần để tránh filter quá dài. ──
        var sourcesByPoolItem = {}; // poolItemId -> [{ internalid, pr, lineIdx, qty }]
        if (rowsToSave.length > 0) {
          var CH1 = 100;
          for (var b1 = 0; b1 < rowsToSave.length; b1 += CH1) {
            var idBatch1 = [];
            for (var b1i = b1; b1i < Math.min(b1 + CH1, rowsToSave.length); b1i++) idBatch1.push(rowsToSave[b1i].poolItemId);
            search.create({
              type: "customrecord_pr_pool_source",
              filters: [["custrecord_pps_pool_item", "anyof", idBatch1]],
              columns: ["internalid", "custrecord_pps_pool_item", "custrecord_pps_pr", "custrecord_pps_prlineidx", "custrecord_pps_qty"]
            }).run().each(function (r) {
              var pidRaw = r.getValue("custrecord_pps_pool_item");
              var pid = String(pidRaw);
              if (!sourcesByPoolItem[pid]) sourcesByPoolItem[pid] = [];
              sourcesByPoolItem[pid].push({
                internalid: r.getValue("internalid"),
                pr:         r.getValue("custrecord_pps_pr"),
                lineIdx:    r.getValue("custrecord_pps_prlineidx"),
                qty:        parseFloat(r.getValue("custrecord_pps_qty")) || 0
              });
              return true;
            });
          }
        }

        // ── BƯỚC 3 — TỐI ƯU: search TOÀN BỘ item_history theo tên trong 1 lần (chỉ cho
        // những dòng có chọn vendor), thay vì search riêng từng dòng. ──
        var histIdByName = {}; // trimmedName -> internalid | null (null = chưa có record)
        var namesNeedHist = [];
        var seenNameHist = {};
        for (var rh = 0; rh < rowsToSave.length; rh++) {
          var rw = rowsToSave[rh];
          if (rw.vendorId && rw.name && !seenNameHist[rw.name]) {
            seenNameHist[rw.name] = true;
            namesNeedHist.push(rw.name);
          }
        }
        if (namesNeedHist.length > 0) {
          var CH2 = 100;
          for (var b2 = 0; b2 < namesNeedHist.length; b2 += CH2) {
            var nameBatch = namesNeedHist.slice(b2, b2 + CH2);
            var orFilters2 = [];
            for (var nf = 0; nf < nameBatch.length; nf++) {
              if (orFilters2.length > 0) orFilters2.push("or");
              orFilters2.push(["name", "is", nameBatch[nf]]);
            }
            search.create({
              type: "customrecord_pr_item_history",
              filters: orFilters2,
              columns: ["name"]
            }).run().each(function (r) {
              histIdByName[(r.getValue("name") || "").trim()] = r.id;
              return true;
            });
          }
        }

        // ── BƯỚC 4: xử lý từng dòng — CHỈ còn 1) submitFields pool_item (không tránh được,
        // không có API update hàng loạt cho custom record) và 2) submitFields pool_source
        // NẾU qty thật sự đổi. Việc ghi PR và item_history được GOM lại, xử lý ở BƯỚC 5/6
        // bên ngoài vòng lặp — để mỗi PR / mỗi item_history chỉ bị load+save ĐÚNG 1 LẦN,
        // dù có bao nhiêu pool item cùng trỏ về nó. ──
        var globalPrUpdatesS = {};   // prId -> [{ lineIdx, itemName, qty }]
        var globalHistUpdatesS = {}; // name -> { vendorId, price, histId }

        for (var rr = 0; rr < rowsToSave.length; rr++) {
          var row = rowsToSave[rr];
          try {
            var poolUpdateValuesS = {
              name:               row.name,
              custrecord_ppi_qty: row.qty
            };
            if (!isNaN(row.price) && row.price > 0) poolUpdateValuesS.custrecord_ppi_rate = row.price;
            record.submitFields({
              type:   "customrecord_pr_pool_item",
              id:     row.poolItemId,
              values: poolUpdateValuesS
            });

            var srcList = sourcesByPoolItem[String(row.poolItemId)] || [];
            var runningQtyS = 0;

            for (var ss = 0; ss < srcList.length; ss++) {
              var src = srcList[ss];
              var lineQtyS;
              if (!row.qtyChanged) {
                lineQtyS = src.qty;
              } else if (ss === srcList.length - 1) {
                lineQtyS = Math.round(row.qty - runningQtyS);
              } else {
                lineQtyS = Math.round((src.qty / row.origQty) * row.qty);
                runningQtyS += lineQtyS;
              }
              if (isNaN(lineQtyS) || lineQtyS < 0) lineQtyS = src.qty;

              if (row.qtyChanged) {
                record.submitFields({
                  type:   "customrecord_pr_pool_source",
                  id:     src.internalid,
                  values: { custrecord_pps_qty: lineQtyS }
                });
              }

              var prIdS = src.pr;
              var lineIdxS = parseInt(src.lineIdx, 10);
              if (!globalPrUpdatesS[prIdS]) globalPrUpdatesS[prIdS] = [];
              globalPrUpdatesS[prIdS].push({ lineIdx: lineIdxS, itemName: row.name, qty: lineQtyS });
            }

            if (row.vendorId && row.name) {
              globalHistUpdatesS[row.name] = {
                vendorId: row.vendorId,
                price:    row.price,
                histId:   histIdByName.hasOwnProperty(row.name) ? histIdByName[row.name] : null
              };
            }

            savedCountS++;
          } catch (rowErrS) {
            errorCountS++;
            log.error("SAVE POOL - ROW ERROR", "idx=" + row.idx + " | " + rowErrS.message);
          }
        }

        // ── BƯỚC 5 — TỐI ƯU: load + save MỖI PR ĐÚNG 1 LẦN, áp dụng tất cả dòng thay đổi
        // (có thể đến từ nhiều pool item khác nhau) cùng lúc — KHÔNG đụng custcol_pr_linked_po
        // / status vì chưa có PO nào. ──
        for (var prIdS2 in globalPrUpdatesS) {
          try {
            var prToUpdateS = record.load({ type: "purchaserequisition", id: prIdS2, isDynamic: false });
            var entriesS = globalPrUpdatesS[prIdS2];
            for (var eu = 0; eu < entriesS.length; eu++) {
              var eS = entriesS[eu];
              prToUpdateS.setSublistValue({ sublistId: "item", fieldId: "custcol_pr_item", line: eS.lineIdx, value: eS.itemName });
              if (eS.qty > 0) {
                prToUpdateS.setSublistValue({ sublistId: "item", fieldId: "quantity", line: eS.lineIdx, value: eS.qty });
              }
            }
            prToUpdateS.save({ ignoreMandatoryFields: true });
          } catch (prErrS) {
            log.error("SAVE POOL - UPDATE PR ERROR", "PR " + prIdS2 + " | " + prErrS.message);
          }
        }

        // ── BƯỚC 6: upsert item_history — mỗi tên item chỉ 1 lần dù xuất hiện ở nhiều dòng
        // (KHÔNG tạo price_log — price_log chỉ ghi khi PO THẬT SỰ được tạo). ──
        for (var nameKeyS in globalHistUpdatesS) {
          try {
            var histEntryS = globalHistUpdatesS[nameKeyS];
            var histValuesS = { custrecord_pr_item_last_vendor: histEntryS.vendorId };
            if (!isNaN(histEntryS.price) && histEntryS.price > 0) histValuesS.custrecord_pr_item_last_purchase_price = histEntryS.price;

            if (histEntryS.histId) {
              record.submitFields({
                type:   "customrecord_pr_item_history",
                id:     histEntryS.histId,
                values: histValuesS
              });
            } else {
              var newHistS = record.create({ type: "customrecord_pr_item_history" });
              newHistS.setValue({ fieldId: "name", value: nameKeyS });
              newHistS.setValue({ fieldId: "custrecord_pr_item_last_vendor", value: histEntryS.vendorId });
              if (!isNaN(histEntryS.price) && histEntryS.price > 0) {
                newHistS.setValue({ fieldId: "custrecord_pr_item_last_purchase_price", value: histEntryS.price });
              }
              newHistS.save();
            }
          } catch (histErrS) {
            log.error("SAVE POOL - ITEM HISTORY ERROR", "item=" + nameKeyS + " | " + histErrS.message);
          }
        }

        // Quay lại đúng trang Create PO — dữ liệu hiển thị sẽ lấy từ pool vừa lưu,
        // origname_i/origqty_i sẽ reset đúng theo giá trị mới (không còn coi là "đã sửa" nữa).
        var backUrlS = url.resolveScript({
          scriptId:     "customscriptsvn_pr_approval_sl",
          deploymentId: "customdeploysvn_pr_approval_sl",
          params: { action: "createpo", recId: recId, saved: savedCountS, saveerr: errorCountS }
        });
        redirect.redirect({ url: backUrlS });
        return;
      } catch (e) {
        log.error("SAVE POOL ERROR", e);
        context.response.write("ERROR: " + e.message);
      }
      return;
    }

    // ─── SUGGEST ITEM ───────────────────────────────────────────
    if (action === "suggestitem") {
      try {
        var keyword = context.request.parameters.keyword || "";
        var results = [];

        var s = search.create({
          type: "customrecord_pr_item_history",
          filters: keyword
            ? [["name", "contains", keyword]]
            : [],
          columns: ["name", "custrecord_pr_item_desc", "custrecord_pr_item_last_purchase_price"]
        });

        s.run().each(function (r) {
          results.push({
            name:  r.getValue("name"),
            desc:  r.getValue("custrecord_pr_item_desc"),
            price: r.getValue("custrecord_pr_item_last_purchase_price") // ── dùng để auto-fill Est. Rate ──
          });
          return results.length < 20;
        });

        context.response.setHeader({
          name: "Content-Type",
          value: "application/json"
        });

        context.response.write(JSON.stringify(results));
      } catch (e) {
        log.error("SUGGEST ERROR", e);
        context.response.write(JSON.stringify([]));
      }
      return;
    }

    // ─── SIGNED PR ───────────────────────────────────────────
    if (action === "uploadform") {
    var html =
      '<html><body style="font-family:sans-serif;padding:20px;">' +
      '<h3>Upload Signed PR (PDF only)</h3>' +
      '<form method="POST" enctype="multipart/form-data" action="' +
        url.resolveScript({
          scriptId: "customscriptsvn_pr_approval_sl",
          deploymentId: "customdeploysvn_pr_approval_sl",
          params: { action: "uploadsubmit", recId: recId }
        }) +
      '">' +
      '<input type="file" name="file" accept="application/pdf" required /><br/><br/>' +
      '<button type="submit">Upload</button>' +
      '</form>' +
      '</body></html>';

    context.response.write(html);
    return;
  }

  if (action === "uploadsubmit") {
    try {
      var uploadedFile = context.request.files.file;

      if (!uploadedFile) {
        context.response.write("No file uploaded");
        return;
      }

      // rename file: original + date
      var now = new Date();
      var dateStr =
        now.getFullYear() +
        ("0" + (now.getMonth() + 1)).slice(-2) +
        ("0" + now.getDate()).slice(-2) + "_" +
        ("0" + now.getHours()).slice(-2) +
        ("0" + now.getMinutes()).slice(-2);

      var originalName = uploadedFile.name.replace(".pdf", "");
      uploadedFile.name = originalName + "_" + dateStr + ".pdf";

      // set folder
      uploadedFile.folder = 104606;

      var fileId = uploadedFile.save();

      // save vào PR
      record.submitFields({
        type: "purchaserequisition",
        id: recId,
        values: {
          custbody_pr_signed_pdf: fileId
        }
      });

      redirect.toRecord({
        type: "purchaserequisition",
        id: recId
      });

    } catch (e) {
      log.error("UPLOAD ERROR", e);
      context.response.write("ERROR: " + e.message);
    }
    return;
  }
  if (action === "downloadsigned") {
    var pr = record.load({ type: "purchaserequisition", id: recId });
    var fileId = pr.getValue({ fieldId: "custbody_pr_signed_pdf" });

    if (!fileId) {
      context.response.write("No file");
      return;
    }

    var f = file.load({ id: fileId });
    context.response.writeFile({ file: f, isInline: false });
    return;
  }
  if (action === "removefile") {
    record.submitFields({
      type: "purchaserequisition",
      id: recId,
      values: {
        custbody_pr_signed_pdf: ""
      }
    });

    redirect.toRecord({
      type: "purchaserequisition",
      id: recId
    });
    return;
  }

  // ─── REMOVE FROM POOL
  if (action === "removefrompool") {
    context.response.setHeader({ name: "Content-Type", value: "application/json" });
    try {
      var poolItemIdToRemove = context.request.parameters.poolItemId;
      if (!poolItemIdToRemove) {
        context.response.write(JSON.stringify({ success: false, message: "Thiếu poolItemId" }));
        return;
      }

      var srcsToDel = search.create({
        type: "customrecord_pr_pool_source",
        filters: [["custrecord_pps_pool_item", "is", poolItemIdToRemove]],
        columns: ["internalid"]
      }).run().getRange({ start: 0, end: 1000 });

      for (var di = 0; di < srcsToDel.length; di++) {
        record.delete({ type: "customrecord_pr_pool_source", id: srcsToDel[di].getValue("internalid") });
      }
      record.delete({ type: "customrecord_pr_pool_item", id: poolItemIdToRemove });

      context.response.write(JSON.stringify({ success: true }));
    } catch (e) {
      log.error("REMOVE FROM POOL ERROR", e);
      context.response.write(JSON.stringify({ success: false, message: e.message }));
    }
    return;
  }
    redirect.toRecord({ type: "purchaserequisition", id: recId });
  }

  return { onRequest: onRequest };
});