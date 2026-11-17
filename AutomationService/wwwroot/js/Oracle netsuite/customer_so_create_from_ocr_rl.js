/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 * @description SuiteScript 2.1 RESTlet for creating Sales Order (SO) in NetSuite from OCR / Inbound Data.
 * 
 * Business Logic ("SO in ERP"):
 * 1. Choose custom form --- Sigma Worldwide Sales order
 * 2. Customer --- collect from customer PO
 * 3. Tax Code --- Choose 0 for SW
 * 4. Currency, Exchange rate, terms --- automatically created once choose customer
 * 5. PO# --- collect from customer PO
 * 6. Date --- Need fill in
 * 7. Person in charge --- input the DRI PM (for further follow up)
 * 8. Subsidiary --- Choose Sigma Worldwide
 * 9. Location --- Choose Sigma Worldwide
 * 10. Project code --- must input (3 digits)
 * 11. PO type --- choose MP or NPI
 * 12. PR type --- Choose DRI for purchasing team
 * 13. Email-Receipt --- created automatically in ERP
 * 14. Choose ALLOW CROSS-SUBSIDIARY FULFILLMENT
 * 15. Expected ship Date --- need to fill in for each line
 * 16. Inventory Location --- Choose Sigma Worldwide (ID: 105)
 */
define(['N/record', 'N/search', 'N/log', 'N/url', 'N/query'], (record, search, log, url, query) => {

    const post = (requestBody) => {
        try {
            log.audit('Step 0: Start SO Creation', 'Bắt đầu nhận và xử lý request tạo Sales Order theo chuẩn SO in ERP');
            log.audit('Request Body Raw', JSON.stringify(requestBody));

            if (!requestBody || !requestBody.header || !requestBody.line_items) {
                log.error('Step 0 Error', 'JSON dữ liệu không hợp lệ, thiếu header hoặc line_items.');
                return JSON.stringify({
                    success: false,
                    message: 'JSON dữ liệu không hợp lệ, thiếu header hoặc line_items.'
                });
            }

            const header = requestBody.header;
            const lineItems = requestBody.line_items;

            // =========================================================
            // 1. CHOOSE CUSTOM FORM: "Sigma Worldwide Sales order" (No. 1)
            // =========================================================
            log.audit('Step 1: Custom Form', 'Đang xác định Custom Form "Sigma Worldwide Sales order"');
            let customFormId = 111; // ID chuẩn của "Sigma Worldwide Sales Order" trên NetSuite Sandbox
            if (header.customform) {
                let parsedForm = parseInt(header.customform, 10);
                if (!isNaN(parsedForm) && parsedForm > 0) {
                    customFormId = parsedForm;
                } else if (typeof header.customform === 'string' && !header.customform.toLowerCase().includes('worldwide')) {
                    let lookedUpForm = getCustomFormIdByName(header.customform);
                    if (lookedUpForm) customFormId = lookedUpForm;
                }
            }
            log.audit('Step 1 Result', 'Custom Form ID: ' + customFormId);

            // =========================================================
            // 2. CUSTOMER: collect from customer PO (No. 2) - BẮT BUỘC TÌM VÀ CHỌN TRƯỚC TIÊN
            // =========================================================
            log.audit('Step 2: Customer Lookup', 'Tìm Customer từ Customer PO: ' + (header.buyer ? header.buyer.name : header.customer));
            let customerName = header.buyer ? header.buyer.name : (header.customer || '');
            let customerId = null;
            if (customerName) {
                customerId = getCustomerInternalIdByName(customerName);
            }
            if (!customerId && header.customer_id) {
                customerId = parseInt(header.customer_id, 10);
            }

            if (!customerId) {
                log.error('Step 2 Error', 'Không tìm thấy Customer trong hệ thống NetSuite: ' + customerName);
                return JSON.stringify({
                    success: false,
                    message: 'Không tìm thấy Customer trong hệ thống NetSuite: ' + customerName
                });
            }
            log.audit('Step 2 Result', 'Customer ID tìm được: ' + customerId);

            // =========================================================
            // KHỞI TẠO BẢN GHI SALES ORDER DYNAMIC
            // =========================================================
            let defaultInitValues = {
                customform: customFormId
            };

            let soRecord = record.create({
                type: record.Type.SALES_ORDER,
                isDynamic: true,
                defaultValues: defaultInitValues
            });

            // GÁN ENTITY (CUSTOMER) TRƯỚC TIÊN (YÊU CẦU BẮT BUỘC TRÊN NETSUITE ĐỂ ĐƯỢC PHÉP CHỌN SUBSIDIARY & LOCATION)
            soRecord.setValue({ fieldId: 'entity', value: parseInt(customerId, 10) });
            log.audit('Step 2: Customer Set', 'Entity đã set, NetSuite tự động sinh Currency, Exchange Rate, Terms & Email-Receipt.');

            // =========================================================
            // 8. SUBSIDIARY: "Sigma Worldwide" (No. 8) - BẮT BUỘC CHỌN SIGMA WORLDWIDE (ID: 1)
            // =========================================================
            log.audit('Step 8: Subsidiary', 'Đang xác định Subsidiary "Sigma Worldwide" (ID: 1)');
            let targetSubsidiaryId = 1; // Luôn luôn là 1 (Sigma Worldwide) theo quy định SO in ERP Rule 8

            let currentSub = null;
            try {
                currentSub = soRecord.getValue({ fieldId: 'subsidiary' });
                log.audit('Customer Primary Subsidiary', 'Subsidiary hiện tại sau khi set Customer: ' + currentSub);
            } catch (e) {}

            try {
                if (parseInt(currentSub, 10) !== targetSubsidiaryId) {
                    soRecord.setValue({ fieldId: 'subsidiary', value: targetSubsidiaryId });
                    log.audit('Step 8 Result', 'Đã gán Subsidiary sang Sigma Worldwide (ID: 1)');
                } else {
                    log.audit('Step 8 Result', 'Subsidiary đã tự động khớp Sigma Worldwide (ID: 1)');
                }
            } catch (subErr) {
                log.error('Step 8 Notice', 'Lỗi khi gán Subsidiary 1: ' + subErr.message);
            }
            let actualSubId = 1;

            // =========================================================
            // 9. LOCATION: "Sigma Worldwide" (No. 9) - BẮT BUỘC CHỌN SIGMA WORLDWIDE (ID: 105)
            // =========================================================
            log.audit('Step 9: Location', 'Đang thiết lập Location "Sigma Worldwide" (ID: 105)');
            let targetLocationId = 105; // ID của Location "Sigma Worldwide"
            try {
                soRecord.setValue({ fieldId: 'location', value: targetLocationId });
                log.audit('Step 9 Result', 'Location Header ID: ' + targetLocationId);
            } catch (locErr) {
                log.error('Step 9 Set Location Error', locErr.message);
            }

            // Nếu request có truyền currency riêng và muốn override
            if (header.currency) {
                let currId = getCurrencyIdByName(header.currency);
                if (currId) {
                    try { soRecord.setValue({ fieldId: 'currency', value: parseInt(currId, 10) }); } catch (currErr) {}
                }
            }

            // =========================================================
            // 5. PO# : collect from customer PO (No. 5)
            // =========================================================
            let poNum = header.po_number || header.customer_po_number || header.ref_po || '';
            if (poNum) {
                soRecord.setValue({ fieldId: 'otherrefnum', value: poNum });
                setCustomFieldIfExists(soRecord, 'custbody_sig_customerpo', poNum);
                log.audit('Step 5: PO# Set', 'PO# (otherrefnum & custbody_sig_customerpo) -> ' + poNum);
            }

            // =========================================================
            // 6. DATE: Need fill in (No. 6)
            // =========================================================
            let soDateVal = header.so_date || header.date || header.po_date;
            let tranDate = parseDate(soDateVal) || new Date();
            soRecord.setValue({ fieldId: 'trandate', value: tranDate });
            log.audit('Step 6: Date Set', 'TranDate -> ' + tranDate.toISOString());

            // =========================================================
            // 7. PERSON IN CHARGE: input the DRI PM (No. 7) -> custbody_sig_pic & salesrep
            // =========================================================
            let driPm = header.person_in_charge || header.dri_pm || '';
            let driPmId = header.person_in_charge_id || header.dri_pm_id || null;
            let empId = null;

            if (driPmId) {
                let parsedEmpId = parseInt(driPmId, 10);
                if (!isNaN(parsedEmpId) && parsedEmpId > 0) {
                    empId = parsedEmpId;
                }
            }
            if (!empId && driPm) {
                empId = getEmployeeIdByName(driPm);
            }

            if (empId) {
                try {
                    soRecord.setValue({ fieldId: 'custbody_sig_pic', value: parseInt(empId, 10) });
                    log.audit('Step 7 Result', `Đã gán custbody_sig_pic = ${empId} (${driPm})`);
                } catch (picErr) {
                    log.error('Step 7 Error setting custbody_sig_pic', picErr.message);
                }

                try {
                    soRecord.setValue({ fieldId: 'salesrep', value: parseInt(empId, 10) });
                    log.audit('Step 7 SalesRep', `Đã gán salesrep = ${empId}`);
                } catch (srErr) {
                    log.debug('Step 7 SalesRep Notice', srErr.message);
                }
            } else if (driPm) {
                log.audit('Step 7 Notice', `Không tìm thấy Employee ID tương ứng với "${driPm}", thử gán dạng text.`);
                setCustomFieldIfExists(soRecord, 'custbody_dri_pm', driPm);
                setCustomFieldIfExists(soRecord, 'custbody_person_in_charge', driPm);
            }

            // =========================================================
            // 10. PROJECT CODE: must input (3 digits) (No. 10)
            // =========================================================
            let projCode = header.project_code || header.project_code_str || '';
            let projCodeId = header.project_code_id ? parseInt(header.project_code_id, 10) : null;
            if (!projCodeId && projCode) {
                projCodeId = getProjectCodeId(projCode);
            }
            if (projCodeId) {
                soRecord.setValue({ fieldId: 'cseg_sig_projcode', value: parseInt(projCodeId, 10) });
                log.audit('Step 10: Project Code Set', `Project Code ID: ${projCodeId} (Raw: ${projCode})`);
            } else if (projCode) {
                setCustomFieldIfExists(soRecord, 'cseg_sig_projcode', projCode);
            }

            // =========================================================
            // 11. PO TYPE: choose MP or NPI (No. 11) (1 = MP, 2 = NPI)
            // =========================================================
            let poType = header.po_type || 'MP';
            let poTypeValue = (poType === 'NPI' || poType === '2' || poType === 2) ? 2 : 1;
            try {
                soRecord.setValue({ fieldId: 'custbody_po_type', value: poTypeValue });
                log.audit('Step 11: PO Type Set', `custbody_po_type -> ${poTypeValue} (${poType})`);
            } catch (poTypeErr) {
                setCustomFieldIfExists(soRecord, 'custbody_po_type', poType);
            }

            // =========================================================
            // 12. PR TYPE: Choose DRI for purchasing team (No. 12)
            // =========================================================
            let prType = header.pr_type || 'DRI';
            setCustomFieldIfExists(soRecord, 'custbody_pr_type', prType);
            setCustomFieldIfExists(soRecord, 'custbody_sig_prtype', prType);
            log.audit('Step 12: PR Type Set', 'PR Type -> ' + prType);

            // =========================================================
            // 14. ALLOW CROSS-SUBSIDIARY FULFILLMENT (No. 14) -> isCrossSubTransaction = true
            // =========================================================
            log.audit('Step 14: Cross-Subsidiary Fulfillment', 'Bật cờ ALLOW CROSS-SUBSIDIARY FULFILLMENT (isCrossSubTransaction)');
            try {
                soRecord.setValue({ fieldId: 'isCrossSubTransaction', value: true });
                log.audit('Step 14 Result', 'isCrossSubTransaction đã bật: true');
            } catch (crossErr) {
                log.error('Step 14 Error setting isCrossSubTransaction', crossErr.message);
                setBooleanFieldIfExists(soRecord, 'crosssubfulfillment', true);
            }

            // =========================================================
            // TÌM TAX CODE 0 CHO SIGMA WORLDWIDE (No. 3) & INVENTORY LOCATION "Sigma Worldwide" (No. 16)
            // =========================================================
            let taxCodeZeroId = header.tax_code ? parseInt(header.tax_code, 10) : getTaxCodeZeroId(1);
            if (!taxCodeZeroId) taxCodeZeroId = 6; // Default to 6 (VAT_HK:UNDEF-HK, 0%)
            log.audit('Step 3: Tax Code 0 ID', 'Tax Code ID: ' + taxCodeZeroId);

            let inventoryLocationId = 105; // Default to 105 (Sigma Worldwide) theo Rule 16
            if (header.inventory_location) {
                let parsedInvLoc = parseInt(header.inventory_location, 10);
                if (!isNaN(parsedInvLoc) && parsedInvLoc > 0) {
                    inventoryLocationId = parsedInvLoc;
                } else if (typeof header.inventory_location === 'string') {
                    if (header.inventory_location.toLowerCase().includes('asia')) {
                        inventoryLocationId = 2;
                    } else if (header.inventory_location.toLowerCase().includes('worldwide')) {
                        inventoryLocationId = 105;
                    }
                }
            }
            log.audit('Step 16: Inventory Location ID', 'Inventory Location ID: ' + inventoryLocationId);

            // =========================================================
            // DUYỆT TỪNG DÒNG HÀNG HÓA (LINE ITEMS)
            // =========================================================
            log.audit('Lines Loop Start', `Bắt đầu thêm ${lineItems.length} chi tiết mặt hàng`);
            for (let i = 0; i < lineItems.length; i++) {
                let line = lineItems[i];
                let itemSku = line.item || line.item_no;
                let itemId = getItemInternalId(itemSku);

                if (!itemId) {
                    log.error(`Line [${i + 1}] Error`, `Không tìm thấy Item SKU: ${itemSku}`);
                    continue;
                }

                soRecord.selectNewLine({ sublistId: 'item' });
                soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'item', value: parseInt(itemId, 10) });

                let qty = parseFloat(line.quantity) || 1;
                let rate = parseFloat(line.price_each) || 0;
                let amount = parseFloat(line.amount) || (qty * rate);

                soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'quantity', value: qty });
                soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'rate', value: rate });
                soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'amount', value: amount });

                if (line.description) {
                    soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'description', value: line.description });
                }

                // 3. Tax Code: Choose 0 for SW (No. 3)
                if (taxCodeZeroId) {
                    try {
                        soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'taxcode', value: taxCodeZeroId });
                    } catch (taxErr) {
                        log.error(`Line [${i + 1}] TaxCode Error`, taxErr.message);
                    }
                }

                // 15. Expected ship Date: need to fill in for each line (No. 15)
                let lineShipDateVal = line.expected_ship_date || line.eta_date || soDateVal;
                let lineShipDate = parseDate(lineShipDateVal);
                if (lineShipDate) {
                    try {
                        soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'expectedshipdate', value: lineShipDate });
                    } catch (shipErr) {
                        log.error(`Line [${i + 1}] ShipDate Error`, shipErr.message);
                    }
                }

                // 16. Inventory Location: Choose Sigma Worldwide (No. 16)
                if (inventoryLocationId) {
                    try {
                        soRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'inventorylocation', value: inventoryLocationId });
                    } catch (invLocErr) {
                        log.error(`Line [${i + 1}] Inventory Location Error`, invLocErr.message);
                    }
                }

                soRecord.commitLine({ sublistId: 'item' });
                log.audit(`Line [${i + 1}] Added`, `SKU: ${itemSku} | Qty: ${qty} | Rate: ${rate} | ShipDate: ${lineShipDateVal} | InvLoc: ${inventoryLocationId}`);
            }

            // =========================================================
            // SAVE RECORD VÀ TRẢ KẾT QUẢ
            // =========================================================
            log.audit('Saving SO Record', 'Đang gọi soRecord.save()...');
            let soId = soRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: false
            });
            log.audit('SO Saved Successfully', `Sales Order ID: ${soId}`);

            // Lấy Document Number (tranid)
            let soFields = search.lookupFields({
                type: search.Type.SALES_ORDER,
                id: soId,
                columns: ['tranid']
            });
            let soTranId = soFields.tranid || ('SO-' + poNum);

            let relativeUrl = url.resolveRecord({
                recordType: record.Type.SALES_ORDER,
                recordId: soId,
                isEditMode: false
            });

            let domain = url.resolveDomain({
                hostType: url.HostType.APPLICATION
            });
            let absoluteSoUrl = 'https://' + domain + relativeUrl;

            return JSON.stringify({
                success: true,
                message: 'Tạo Sales Order trên NetSuite thành công!',
                soId: soId,
                soNumber: soTranId,
                soUrl: absoluteSoUrl
            });

        } catch (e) {
            log.error('CRITICAL ERROR TRACE', `Lỗi tạo Sales Order: ${e.message} | Stack: ${JSON.stringify(e)}`);
            return JSON.stringify({
                success: false,
                message: 'Lỗi khi tạo Sales Order trên NetSuite: ' + e.message
            });
        }
    };

    // =========================================================
    // HELPER FUNCTIONS
    // =========================================================

    function getCustomFormIdByName(formName) {
        if (!formName) return null;
        try {
            let sql = `SELECT id, name FROM customform WHERE UPPER(name) LIKE UPPER(?)`;
            let res = query.runSuiteQL({ query: sql, params: ['%' + formName + '%'] }).asMappedResults();
            if (res && res.length > 0) return parseInt(res[0].id, 10);
        } catch (e) {
            log.debug('getCustomFormIdByName SuiteQL error, fallback', e.message);
        }
        return null;
    }

    function getCustomerInternalIdByName(name) {
        if (!name) return null;
        let customerId = null;
        let s = search.create({
            type: search.Type.CUSTOMER,
            filters: [
                ['companyname', 'is', name],
                'OR',
                ['entityid', 'is', name],
                'OR',
                ['companyname', 'contains', name]
            ],
            columns: ['internalid']
        });
        s.run().each((res) => { customerId = res.getValue({ name: 'internalid' }); return false; });
        return customerId;
    }

    function getSubsidiaryIdByName(name) {
        if (!name) return null;
        try {
            let sql = `SELECT id FROM subsidiary WHERE UPPER(name) LIKE UPPER(?) AND isinactive = 'F' ORDER BY id ASC`;
            let res = query.runSuiteQL({ query: sql, params: ['%' + name + '%'] }).asMappedResults();
            if (res && res.length > 0) return parseInt(res[0].id, 10);
        } catch (e) {
            log.debug('getSubsidiaryIdByName SuiteQL error', e.message);
        }
        return null;
    }

    function getLocationIdByName(name, subId) {
        if (!name) return null;
        try {
            let sql = `SELECT id FROM location WHERE UPPER(name) LIKE UPPER(?) AND isinactive = 'F'`;
            let params = ['%' + name + '%'];
            if (subId) {
                sql += ` AND subsidiary = ?`;
                params.push(subId);
            }
            sql += ` ORDER BY id ASC`;
            let res = query.runSuiteQL({ query: sql, params: params }).asMappedResults();
            if (res && res.length > 0) return parseInt(res[0].id, 10);
        } catch (e) {
            log.debug('getLocationIdByName SuiteQL error', e.message);
        }
        return null;
    }

    function getTaxCodeZeroId(subId) {
        try {
            let s = search.create({
                type: search.Type.SALES_TAX_ITEM,
                filters: [
                    ['rate', 'is', '0%'],
                    'OR',
                    ['itemid', 'contains', '0']
                ],
                columns: ['internalid', 'rate', 'itemid']
            });
            let taxId = null;
            s.run().each((res) => {
                taxId = res.getValue({ name: 'internalid' });
                return false;
            });
            if (taxId) return parseInt(taxId, 10);
        } catch (e) {
            log.debug('getTaxCodeZeroId search error', e.message);
        }
        return null;
    }

    function getProjectCodeId(code) {
        if (!code) return null;
        let projId = null;

        // 1. Thử tìm theo internalid trực tiếp nếu là số
        try {
            let sId = search.create({
                type: 'customrecord_cseg_sig_projcode',
                filters: [
                    ['internalid', 'is', code.toString()]
                ],
                columns: ['internalid']
            });
            sId.run().each((res) => { projId = res.getValue({ name: 'internalid' }); return false; });
            if (projId) return projId;
        } catch (e) {}

        // 2. Thử tìm theo name chứa hoặc khớp mã
        try {
            let s = search.create({
                type: 'customrecord_cseg_sig_projcode',
                filters: [
                    ['name', 'is', code.toString()],
                    'OR',
                    ['name', 'contains', code.toString()]
                ],
                columns: ['internalid']
            });
            s.run().each((res) => { projId = res.getValue({ name: 'internalid' }); return false; });
        } catch (e) {
            log.debug('getProjectCodeId error', e.message);
        }
        return projId;
    }

    function getEmployeeIdByName(name) {
        if (!name) return null;
        name = name.toString().trim();
        if (!name) return null;

        // Nếu chuỗi có dạng "Alex Chen (2703)" hoặc là ID số nguyên trực tiếp
        let idMatch = name.match(/\b(\d{3,6})\b/);
        if (idMatch) {
            return parseInt(idMatch[1], 10);
        }

        // Bảng tra cứu trực tiếp các nhân sự DRI PM phổ biến trên NetSuite Sandbox
        const knownStaff = {
            'alex chen': 2703,
            'owen zhang': 2702,
            'leon liao': 2704,
            'sean jin': 14996,
            'jack liu': 24439,
            'yoyo huang': 2700,
            'james kung': 8450,
            'kenneth tang': 2701,
            'charlie mcarthur': 3396,
            'alexander cobb': 2686,
            'beth guo': 2645
        };
        let lowerName = name.toLowerCase();
        for (let key in knownStaff) {
            if (lowerName.includes(key)) {
                return knownStaff[key];
            }
        }

        // 1. Tìm qua SuiteQL trên entity
        try {
            let sql = `SELECT id FROM entity WHERE isperson = 'T' AND isinactive = 'F' AND (UPPER(altname) LIKE UPPER(?) OR UPPER(entityid) LIKE UPPER(?)) ORDER BY id ASC`;
            let res = query.runSuiteQL({ query: sql, params: ['%' + name + '%', '%' + name + '%'] }).asMappedResults();
            if (res && res.length > 0) return parseInt(res[0].id, 10);
        } catch (e) {
            log.debug('getEmployeeIdByName SuiteQL error, fallback to search', e.message);
        }

        // 2. Tìm qua N/search trên employee
        try {
            let s = search.create({
                type: search.Type.EMPLOYEE,
                filters: [
                    ['isinactive', 'is', 'F'],
                    'AND',
                    [
                        ['entityid', 'contains', name],
                        'OR',
                        ['firstname', 'contains', name],
                        'OR',
                        ['lastname', 'contains', name]
                    ]
                ],
                columns: ['internalid']
            });
            let empId = null;
            s.run().each((res) => { empId = res.getValue({ name: 'internalid' }); return false; });
            if (empId) return parseInt(empId, 10);
        } catch (e) {
            log.debug('getEmployeeIdByName Search error', e.message);
        }

        return null;
    }

    function getCurrencyIdByName(code) {
        if (!code) return null;
        let currId = null;
        try {
            let s = search.create({
                type: search.Type.CURRENCY,
                filters: [['symbol', 'is', code], 'OR', ['name', 'is', code]],
                columns: ['internalid']
            });
            s.run().each((res) => { currId = res.getValue({ name: 'internalid' }); return false; });
        } catch (e) {}
        return currId;
    }

    function getItemInternalId(itemSku) {
        if (!itemSku) return null;
        let itemId = null;
        let s = search.create({
            type: search.Type.ITEM,
            filters: [['itemid', 'is', itemSku]],
            columns: ['internalid']
        });
        s.run().each((res) => { itemId = res.getValue({ name: 'internalid' }); return false; });
        return itemId;
    }

    function setCustomFieldIfExists(rec, fieldId, value) {
        try {
            rec.setValue({ fieldId: fieldId, value: value });
        } catch (err) {
            log.debug('setCustomFieldIfExists Warning', `Không thể gán ${fieldId}: ${err.message}`);
        }
    }

    function setBooleanFieldIfExists(rec, fieldId, value) {
        try {
            rec.setValue({ fieldId: fieldId, value: value === true });
        } catch (err) {
            log.debug('setBooleanFieldIfExists Warning', `Không thể gán cờ ${fieldId}: ${err.message}`);
        }
    }

    function parseDate(dateStr) {
        if (!dateStr) return null;
        try {
            let s = dateStr.toString().substring(0, 10);
            let parts = s.split('-');
            if (parts.length === 3) {
                return new Date(parseInt(parts[0], 10), parseInt(parts[1], 10) - 1, parseInt(parts[2], 10));
            }
        } catch (e) {}
        return null;
    }

    return { post };
});
