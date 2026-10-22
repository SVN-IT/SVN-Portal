/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const post = (requestBody) => {
        try {
            log.audit('Incoming PO Payload', JSON.stringify(requestBody));

            if (!requestBody || !requestBody.header || !requestBody.line_items) {
                return JSON.stringify({
                    success: false,
                    message: 'JSON dữ liệu không hợp lệ, thiếu header hoặc line_items.'
                });
            }

            const header = requestBody.header;
            const lineItems = requestBody.line_items;

            // 1. Gán cứng hoặc tìm an toàn Vendor (VD-0005)
            let vendorId = getInternalIdByField(search.Type.VENDOR, 'entityid', 'VD-0005');
            if (!vendorId) {
                vendorId = getInternalIdByField(search.Type.VENDOR, 'companyname', 'Sigma Vietnam');
            }
            log.audit('DEBUG VENDOR ID', 'Vendor ID tìm được là: ' + vendorId); // Thêm dòng này để check trong Execution Log

            if (!vendorId) {
                return JSON.stringify({ success: false, message: 'Không tìm thấy Vendor mặc định: VD-0005' });
            }

            // 2. Tìm Original Customer theo tên buyer
            let originalCustomerId = null;
            if (header.buyer && header.buyer.name) {
                originalCustomerId = getCustomerInternalIdByName(header.buyer.name);
            }

            // 3. Tìm Payment Terms
            let termsId = null;
            if (header.payment_terms) {
                termsId = getTermIdByName(header.payment_terms);
            }

            // 4. Tìm Location ID cho "Sigma Asia"
            let locationId = getLocationIdByName('Sigma Asia');

            // 5. Khởi tạo bản ghi Purchase Order
            let poRecord = record.create({
                type: record.Type.PURCHASE_ORDER,
                isDynamic: true
            });

            // Nếu bạn biết chính xác Internal ID của custom form "Sigma Purchase Order - Linked", 
            // hãy điền số vào thay vì gọi search (Ví dụ: poRecord.setValue({ fieldId: 'customform', value: 123 }));
            // Nếu form đã là mặc định, bạn có thể comment/bỏ dòng set customform đi.

            if (vendorId) {
                poRecord.setValue({
                    fieldId: 'entity',
                    value: parseInt(vendorId, 10) // Ép kiểu sang số nguyên để NetSuite nhận diện chuẩn xác
                });
            }

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
                poRecord.setValue({ fieldId: 'terms', value: termsId });
            }

            if (locationId) {
                poRecord.setValue({ fieldId: 'location', value: locationId });
            }

            setCustomFieldIfExists(poRecord, 'custbody_project_code', 'CAL');
            setCustomFieldIfExists(poRecord, 'custbody_po_type', 'MP');

            // 6. Duyệt qua các dòng Line Items
            for (let i = 0; i < lineItems.length; i++) {
                let line = lineItems[i];
                let itemId = getItemInternalId(line.item);

                if (!itemId) {
                    log.error('Item Not Found', `Không tìm thấy Item SKU: ${line.item}`);
                    continue;
                }

                poRecord.selectNewLine({ sublistId: 'item' });

                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'item', value: itemId });
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

            // 7. Lưu bản ghi PO
            let poId = poRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: false
            });

            // Lấy mã số PO hiển thị thực tế (tranid)
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
            log.error('Main RESTlet Critical Error', e.message);
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