/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const post = (requestBody) => {
        try {
            log.audit('Step 0', 'Bắt đầu nhận request');

            if (!requestBody || !requestBody.header || !requestBody.line_items) {
                return JSON.stringify({
                    success: false,
                    message: 'JSON dữ liệu không hợp lệ, thiếu header hoặc line_items.'
                });
            }

            const header = requestBody.header;
            const lineItems = requestBody.line_items;

            // 1. Trace tìm Vendor
            log.audit('Step 1', 'Đang tìm Vendor VD-0005');
            let vendorId = getInternalIdByField(search.Type.VENDOR, 'entityid', 'VD-0005');
            if (!vendorId) {
                vendorId = getInternalIdByField(search.Type.VENDOR, 'companyname', 'Sigma Vietnam');
            }
            log.audit('Step 1 Result', 'Vendor ID tìm được: ' + vendorId);

            if (!vendorId) {
                return JSON.stringify({ success: false, message: 'Không tìm thấy Vendor mặc định: VD-0005' });
            }

            // 2. Trace lookup Subsidiary của Vendor và check ưu tiên "Sigma Asia"
            log.audit('Step 2', 'Đang lookup Subsidiary của Vendor và kiểm tra Sigma Asia');

            let vendorData = search.lookupFields({
                type: search.Type.VENDOR,
                id: vendorId,
                columns: ['subsidiary']
            });

            log.audit('Step 2 Debug - Raw List', JSON.stringify(vendorData.subsidiary));

            // Tìm Internal ID của "Sigma Asia" trong hệ thống
            // Tìm kiếm linh hoạt hơn cho Subsidiary "Sigma Asia" (dùng contains thay vì is)
            let sigmaAsiaId = null;
            let subSearch = search.create({
                type: search.Type.SUBSIDIARY,
                filters: [['name', 'contains', 'Sigma Asia']],
                columns: ['internalid', 'name']
            });

            subSearch.run().each((res) => {
                sigmaAsiaId = res.getValue({ name: 'internalid' });
                let subName = res.getValue({ name: 'name' });
                log.audit('Found Subsidiary Match', `ID: ${sigmaAsiaId} - Name: ${subName}`);
                return false; // Lấy kết quả đầu tiên khớp
            });
            log.audit('Step 2 Debug - Sigma Asia ID', 'ID tìm được của Sigma Asia là: ' + sigmaAsiaId);

            let vendorSubsidiaryId = 1; // Giá trị mặc định an toàn nếu không tìm thấy gì

            //if (vendorData && vendorData.subsidiary && vendorData.subsidiary.length > 0) {
            //    let foundSigmaAsia = false;

            //    // Duyệt qua danh sách subsidiary mà vendor này thuộc về
            //    if (sigmaAsiaId) {
            //        for (let i = 0; i < vendorData.subsidiary.length; i++) {
            //            if (vendorData.subsidiary[i].value == sigmaAsiaId) {
            //                vendorSubsidiaryId = sigmaAsiaId;
            //                foundSigmaAsia = true;
            //                break;
            //            }
            //        }
            //    }

            //    // Nếu vendor không có Sigma Asia, lấy tạm subsidiary đầu tiên trong danh sách của nó
            //    if (!foundSigmaAsia) {
            //        vendorSubsidiaryId = vendorData.subsidiary[0].value;
            //    }
            //}

            if (sigmaAsiaId != null) {
                vendorSubsidiaryId = sigmaAsiaId;
            }

            log.audit('Step 2 Result', 'Subsidiary ID cuối cùng được chọn: ' + vendorSubsidiaryId);

            // 3. Trace tìm Customer
            log.audit('Step 3', 'Đang tìm Original Customer');
            let originalCustomerId = null;
            if (header.buyer && header.buyer.name) {
                originalCustomerId = getCustomerInternalIdByName(header.buyer.name);
            }
            log.audit('Step 3 Result', 'Customer ID: ' + originalCustomerId);

            // 4. Trace tìm Terms
            log.audit('Step 4', 'Đang tìm Payment Terms');
            let termsId = null;
            if (header.payment_terms) {
                termsId = getTermIdByName(header.payment_terms);
            }
            log.audit('Step 4 Result', 'Terms ID: ' + termsId);

            // 5. Trace tìm Location
            log.audit('Step 5', 'Đang tìm Location Sigma Asia');
            let locationId = getLocationIdByName('Sigma Asia');
            log.audit('Step 5 Result', 'Location ID: ' + locationId);

            // 6. Trace khởi tạo bản ghi PO
            log.audit('Step 6', 'Đang khởi tạo bản ghi Purchase Order');
            let poRecord = record.create({
                type: record.Type.PURCHASE_ORDER,
                isDynamic: true,
                defaultValues: {
                    customform: 102,
                    subsidiary: parseInt(vendorSubsidiaryId, 10)
                }
            });
            log.audit('Step 6 Result', 'Khởi tạo PO thành công, đang set Header...');

            poRecord.setValue({
                fieldId: 'entity',
                value: parseInt(vendorId, 10)
            });

            let defaultLocation = poRecord.getValue({ fieldId: 'location' });
            log.audit('Check Default Location', 'Location mặc định sau khi tạo PO là: ' + defaultLocation);

            //if (locationId) {
            //    poRecord.setValue({ fieldId: 'location', value: parseInt(locationId, 10) });
            //}

            if (originalCustomerId) {
                setCustomFieldIfExists(poRecord, 'custbody_original_customer', originalCustomerId);
            }

            if (header.po_number) {
                poRecord.setValue({ fieldId: 'otherrefnum', value: header.po_number });
            }

            if (header.po_date) {
                let dateStr = header.po_date.substring(0, 10);
                let parts = dateStr.split('-');
                if (parts.length === 3) {
                    let poDate = new Date(parts[0], parts[1] - 1, parts[2]);
                    poRecord.setValue({ fieldId: 'trandate', value: poDate });
                }
            }

            if (termsId) {
                poRecord.setValue({ fieldId: 'terms', value: parseInt(termsId, 10) });
            }

            setCustomFieldIfExists(poRecord, 'custbody_project_code', 'CAL');
            setCustomFieldIfExists(poRecord, 'custbody_po_type', 'MP');

            // 7. Trace Line Items
            log.audit('Step 7', 'Bắt đầu duyệt Line Items');
            for (let i = 0; i < lineItems.length; i++) {
                let line = lineItems[i];
                let itemId = getItemInternalId(line.item);

                if (!itemId) {
                    log.error('Item Not Found', `Không tìm thấy Item SKU: ${line.item}`);
                    continue;
                }

                poRecord.selectNewLine({ sublistId: 'item' });
                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'item', value: parseInt(itemId, 10) });
                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'quantity', value: line.quantity });
                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'rate', value: line.price_each });

                if (line.eta_date) {
                    let etaParts = line.eta_date.split('-');
                    if (etaParts.length === 3) {
                        let etaDate = new Date(etaParts[0], etaParts[1] - 1, etaParts[2]);
                        poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'expectedreceiptdate', value: etaDate });
                    }
                }

                poRecord.commitLine({ sublistId: 'item' });
            }

            // 8. Trace Lưu bản ghi
            log.audit('Step 8', 'Đang thực hiện lưu bản ghi PO (poRecord.save)');
            let poId = poRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: false
            });

            let poFields = search.lookupFields({
                type: search.Type.PURCHASE_ORDER,
                id: poId,
                columns: ['tranid']
            });
            let poTranId = poFields.tranid;

            log.audit('PO Created Successfully', `Internal ID: ${poId} - PO Number: ${poTranId}`);

            return JSON.stringify({
                success: true,
                message: 'Tạo Purchase Order thành công!',
                poId: poId,
                poNumber: poTranId
            });

        } catch (e) {
            log.error('CRITICAL ERROR TRACE', 'Lỗi tại: ' + e.message + ' | Stack: ' + JSON.stringify(e));
            return JSON.stringify({
                success: false,
                message: 'Lỗi hệ thống: ' + e.message
            });
        }
    };

    // Các hàm helper an toàn
    function getInternalIdByField(type, fieldName, value) {
        let internalId = null;
        let s = search.create({ type: type, filters: [[fieldName, 'is', value]], columns: ['internalid'] });
        s.run().each((res) => { internalId = res.getValue({ name: 'internalid' }); return false; });
        return internalId;
    }

    function getCustomerInternalIdByName(name) {
        let customerId = null;
        let s = search.create({
            type: search.Type.CUSTOMER,
            filters: [['companyname', 'is', name], 'OR', ['entityid', 'is', name]],
            columns: ['internalid']
        });
        s.run().each((res) => { customerId = res.getValue({ name: 'internalid' }); return false; });
        return customerId;
    }

    function getTermIdByName(termName) {
        let termId = null;
        let s = search.create({ type: 'term', filters: [['name', 'contains', termName]], columns: ['internalid'] });
        s.run().each((res) => { termId = res.getValue({ name: 'internalid' }); return false; });
        return termId;
    }

    function getLocationIdByName(locName) {
        let locId = null;
        let s = search.create({ type: 'location', filters: [['name', 'is', locName]], columns: ['internalid'] });
        s.run().each((res) => { locId = res.getValue({ name: 'internalid' }); return false; });
        return locId;
    }

    function getItemInternalId(itemSku) {
        let itemId = null;
        if (itemSku) {
            let s = search.create({ type: search.Type.ITEM, filters: [['itemid', 'is', itemSku]], columns: ['internalid'] });
            s.run().each((res) => { itemId = res.getValue({ name: 'internalid' }); return false; });
        }
        return itemId;
    }

    function setCustomFieldIfExists(rec, fieldId, value) {
        try { rec.setValue({ fieldId: fieldId, value: value }); } catch (err) { }
    }

    return { post };
});