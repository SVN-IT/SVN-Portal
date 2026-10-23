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
            let locationId = getLocationIdByName('Sigma Asia', vendorSubsidiaryId);
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

            // Tự động tìm và set Currency dựa vào header.currency (ví dụ: 'USD', 'VND')
            if (header.currency) {
                let currencyId = getCurrencyIdByName(header.currency);
                if (currencyId) {
                    poRecord.setValue({
                        fieldId: 'currency',
                        value: parseInt(currencyId, 10)
                    });
                    log.audit('Currency Set', `Currency: ${header.currency} -> ID: ${currencyId}`);
                } else {
                    log.error('Currency Not Found', `Không tìm thấy Currency với mã: ${header.currency}`);
                }
            }

            // Tìm và set Incoterm dựa trên header.trade_term
            let tradeTerm = header.trade_terms;
            if (tradeTerm) {
                log.audit('Incoterm Debug', 'Đang tìm Incoterm với tên: "' + tradeTerm + '"');

                let incotermField = poRecord.getField({ fieldId: 'incoterm' });
                if (incotermField) {
                    let incotermOptions = incotermField.getSelectOptions();
                    let matchedIncotermId = null;

                    if (incotermOptions && incotermOptions.length > 0) {
                        for (let i = 0; i < incotermOptions.length; i++) {
                            // So sánh khớp chính xác tên (ví dụ: 'DAP' hoặc 'EXW')
                            if (incotermOptions[i].text.trim().toUpperCase() === tradeTerm.trim().toUpperCase()) {
                                matchedIncotermId = incotermOptions[i].value;
                                break;
                            }
                        }
                    }

                    if (matchedIncotermId) {
                        poRecord.setValue({
                            fieldId: 'incoterm',
                            value: parseInt(matchedIncotermId, 10)
                        });
                        log.audit('Incoterm Set', `Trade Terms: ${tradeTerm} -> ID: ${matchedIncotermId}`);
                    } else {
                        log.error('Incoterm Not Found', `Không tìm thấy Incoterm khớp với tên: ${tradeTerm}`);
                    }
                }
            } else {
                log.audit('Incoterm Debug', 'header.trade_terms đang bị trống hoặc undefined!');
            }

            // Tìm và set Terms tổng quát, chống lệch định dạng (Net 60, NET60, net 60, v.v.)
            let paymentTermInput = header.payment_terms;
            if (paymentTermInput) {
                log.audit('Terms Debug', 'Dữ liệu payment_terms nhận từ header: "' + paymentTermInput + '"');

                let termsField = poRecord.getField({ fieldId: 'terms' });
                if (termsField) {
                    let termsOptions = termsField.getSelectOptions();
                    let matchedTermId = null;

                    if (termsOptions && termsOptions.length > 0) {
                        let cleanInput = paymentTermInput.toString().replace(/\s+/g, '').toLowerCase(); // VD: "net60"
                        let inputDigits = paymentTermInput.toString().replace(/\D/g, ''); // Lấy riêng phần số: "60"

                        // Tầng 1 & 2: So sánh chuẩn hóa (bỏ qua khoảng trắng và chữ hoa/thường)
                        for (let i = 0; i < termsOptions.length; i++) {
                            let optText = termsOptions[i].text;
                            let cleanOptText = optText.replace(/\s+/g, '').toLowerCase(); // VD: "net60"

                            if (cleanOptText === cleanInput) {
                                matchedTermId = termsOptions[i].value;
                                log.audit('Terms Match Found (Exact/Clean)', `Khớp chuỗi "${optText}" với input "${paymentTermInput}"`);
                                break;
                            }
                        }

                        // Tầng 3: Nếu chưa khớp, thử dò theo con số (ví dụ input có số 60 -> tìm option chứa số 60)
                        if (!matchedTermId && inputDigits !== '') {
                            for (let i = 0; i < termsOptions.length; i++) {
                                let optText = termsOptions[i].text;
                                let optDigits = optText.replace(/\D/g, ''); // Lấy số của option trên NetSuite

                                if (optDigits === inputDigits) {
                                    matchedTermId = termsOptions[i].value;
                                    log.audit('Terms Match Found (By Digits)', `Khớp số "${inputDigits}" qua option "${optText}"`);
                                    break;
                                }
                            }
                        }
                    }

                    if (matchedTermId) {
                        poRecord.setValue({
                            fieldId: 'terms',
                            value: parseInt(matchedTermId, 10)
                        });
                        log.audit('Terms Set Success', `Terms ID: ${matchedTermId}`);
                    } else {
                        log.error('Terms Not Found', `Không thể tìm thấy Terms phù hợp cho input: "${paymentTermInput}"`);
                    }
                }
            } else {
                log.audit('Terms Debug', 'header.payment_terms đang bị trống hoặc undefined!');
            }

            // Kiểm tra các option có sẵn trong trường location của bản ghi PO vừa tạo
            //let locationField = poRecord.getField({ fieldId: 'location' });
            //if (locationField) {
            //    let options = locationField.getSelectOptions();
            //    for (let i = 0; i < options.length; i++) {
            //        log.audit('Location Option Available', `ID: ${options[i].value} - Text: ${options[i].text}`);
            //    }
            //}

            //let defaultLocation = poRecord.getValue({ fieldId: 'location' });
            //log.audit('Check Default Location', 'Location mặc định sau khi tạo PO là: ' + defaultLocation);

            //if (locationId) {
            //    poRecord.setValue({ fieldId: 'location', value: parseInt(locationId, 10) });
            //}

            // Tự động lấy Location đầu tiên đang có sẵn trong select options của bản ghi hiện tại
            let locationField = poRecord.getField({ fieldId: 'location' });
            if (locationField) {
                let options = locationField.getSelectOptions();
                if (options && options.length > 0) {
                    let validLocationId = options[0].value;
                    log.audit('Auto Selected Location', `ID: ${validLocationId} - Text: ${options[0].text}`);

                    poRecord.setValue({
                        fieldId: 'location',
                        value: parseInt(validLocationId, 10)
                    });
                } else {
                    log.error('Location Error', 'Danh sách location trong select box đang trống!');
                }
            }

            if (originalCustomerId) {
                setCustomFieldIfExists(poRecord, 'custbody_sig_po_original_customer', originalCustomerId);
            }

            if (header.po_number) {
                poRecord.setValue({ fieldId: 'custbody_sig_customerpo', value: header.po_number });
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

            /*setCustomFieldIfExists(poRecord, 'cseg_sig_projcode', 'CAL');*/

            // Thay vì gán cứng chuỗi 'CAL', ta lấy tự động option đầu tiên trong select box của Project Code
            let projectField = poRecord.getField({ fieldId: 'cseg_sig_projcode' });
            if (projectField) {
                let projOptions = projectField.getSelectOptions();
                if (projOptions && projOptions.length > 0) {
                    // Bạn có thể chọn option đầu tiên hoặc tìm đúng tên project bạn muốn
                    let validProjId = projOptions[0].value;
                    log.audit('Auto Selected Project Code', `ID: ${validProjId} - Text: ${projOptions[0].text}`);

                    poRecord.setValue({
                        fieldId: 'cseg_sig_projcode',
                        value: parseInt(validProjId, 10)
                    });
                }
            }

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

    function getLocationIdByName(locName, subsidiaryId) {
        let locId = null;
        let filters = [['name', 'is', locName]];

        // Bổ sung lọc theo subsidiary để phân biệt các kho trùng tên ở các công ty con khác nhau
        if (subsidiaryId) {
            filters.push('AND');
            filters.push(['subsidiary', 'anyof', subsidiaryId]);
        }

        let s = search.create({ type: 'location', filters: filters, columns: ['internalid'] });
        s.run().each((res) => {
            locId = res.getValue({ name: 'internalid' });
            return false;
        });
        return locId;
    }

    function getCurrencyIdByName(currCode) {
        let currId = null;
        if (currCode) {
            let s = search.create({
                type: search.Type.CURRENCY,
                filters: [['symbol', 'is', currCode], 'OR', ['name', 'is', currCode]],
                columns: ['internalid']
            });
            s.run().each((res) => {
                currId = res.getValue({ name: 'internalid' });
                return false;
            });
        }
        return currId;
    }

    function getIncotermIdByName(termName) {
        let termId = null;
        if (termName) {
            let s = search.create({
                type: 'customlist_incoterm', // Hoặc 'incoterm' tùy hệ thống của bạn
                filters: [['name', 'is', termName.trim()]], // Chỉ lấy chính xác tuyệt đối theo tên truyền vào
                columns: ['internalid']
            });
            s.run().each((res) => {
                termId = res.getValue({ name: 'internalid' });
                return false;
            });
        }
        return termId;
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