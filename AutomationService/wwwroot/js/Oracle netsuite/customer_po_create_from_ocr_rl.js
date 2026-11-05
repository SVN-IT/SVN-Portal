/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log', 'N/url'], (record, search, log, url) => {

    const post = (requestBody) => {
        try {
            log.audit('Step 0: Start', 'Bắt đầu nhận và xử lý request tạo PO từ OCR');
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

            // 1. Trace tìm Vendor
            log.audit('Step 1: Vendor Lookup', 'Bắt đầu tìm kiếm Vendor VD-0005 / Sigma Vietnam');
            let vendorId = getInternalIdByField(search.Type.VENDOR, 'entityid', 'VD-0005');
            if (!vendorId) {
                vendorId = getInternalIdByField(search.Type.VENDOR, 'companyname', 'Sigma Vietnam');
            }

            if (!vendorId) {
                log.error('Step 1 Error', 'Không tìm thấy Vendor mặc định VD-0005 / Sigma Vietnam trong hệ thống!');
                return JSON.stringify({ success: false, message: 'Không tìm thấy Vendor mặc định: VD-0005' });
            }
            log.audit('Step 1 Result', 'Vendor ID tìm được: ' + vendorId);

            // 2. Lấy Subsidiary trực tiếp từ request header
            let targetSubsidiaryId = header.subsidiary ? parseInt(header.subsidiary, 10) : 1;
            log.audit('Step 2: Subsidiary', `Subsidiary ID chọn từ request header: ${targetSubsidiaryId} (gốc: ${header.subsidiary})`);

            // 3. Trace tìm Customer (Buyer)
            log.audit('Step 3: Customer Lookup', 'Đang tìm Original Customer với tên: ' + (header.buyer ? header.buyer.name : 'N/A'));
            let originalCustomerId = null;
            if (header.buyer && header.buyer.name) {
                originalCustomerId = getCustomerInternalIdByName(header.buyer.name);
            }
            log.audit('Step 3 Result', 'Original Customer ID tìm được: ' + originalCustomerId);

            // 4. Trace tìm Terms
            log.audit('Step 4: Terms Lookup', 'Đang tìm Payment Terms: ' + header.payment_terms);
            let termsId = null;
            if (header.payment_terms) {
                termsId = getTermIdByName(header.payment_terms);
            }
            log.audit('Step 4 Result', 'Payment Terms ID tìm được: ' + termsId);

            // 5. Lấy Location trực tiếp từ request header
            let targetLocationId = header.location ? parseInt(header.location, 10) : null;
            log.audit('Step 5: Location', `Location ID chọn từ request header: ${targetLocationId} (gốc: ${header.location})`);

            // 6. Trace khởi tạo bản ghi PO
            log.audit('Step 6: Init PO Record', `Đang khởi tạo Purchase Order bản ghi mới (customform: 102, subsidiary: ${targetSubsidiaryId})`);
            let poRecord = record.create({
                type: record.Type.PURCHASE_ORDER,
                isDynamic: true,
                defaultValues: {
                    customform: 102,
                    subsidiary: targetSubsidiaryId
                }
            });

            // Set Entity (Vendor)
            poRecord.setValue({
                fieldId: 'entity',
                value: parseInt(vendorId, 10)
            });
            log.audit('Set Header Field', `Vendor (entity) -> ID: ${vendorId}`);

            // Ép chặt Subsidiary lần nữa
            poRecord.setValue({
                fieldId: 'subsidiary',
                value: targetSubsidiaryId
            });
            log.audit('Set Header Field', `Subsidiary -> ID: ${targetSubsidiaryId}`);

            // Set Location từ request header lên header PO
            if (targetLocationId) {
                poRecord.setValue({
                    fieldId: 'location',
                    value: targetLocationId
                });
                log.audit('Set Header Field', `Location Header -> ID: ${targetLocationId}`);
            } else {
                log.audit('Set Header Field Warning', 'Không có location ID từ header request, bỏ qua gán location header.');
            }

            // Set Currency từ request header
            if (header.currency) {
                let currencyId = getCurrencyIdByName(header.currency);
                if (currencyId) {
                    poRecord.setValue({
                        fieldId: 'currency',
                        value: parseInt(currencyId, 10)
                    });
                    log.audit('Set Header Field', `Currency: ${header.currency} -> ID: ${currencyId}`);
                } else {
                    log.error('Currency Not Found', `Không tìm thấy Currency với mã: ${header.currency}`);
                }
            }

            // Set Incoterm
            let rawTradeTerm = header.trade_terms || header.inco_terms;
            let tradeTerm = rawTradeTerm ? rawTradeTerm.toString().trim().split(' ')[0] : null;

            if (tradeTerm) {
                log.audit('Incoterm Processing', 'Đang dò Incoterm khớp với: "' + tradeTerm + '"');
                let incotermField = poRecord.getField({ fieldId: 'incoterm' });
                if (incotermField) {
                    let incotermOptions = incotermField.getSelectOptions();
                    let matchedIncotermId = null;

                    if (incotermOptions && incotermOptions.length > 0) {
                        for (let i = 0; i < incotermOptions.length; i++) {
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
                        log.audit('Set Header Field', `Incoterm: ${tradeTerm} -> ID: ${matchedIncotermId}`);
                    } else {
                        log.error('Incoterm Match Failed', `Không tìm thấy Incoterm option nào khớp với: ${tradeTerm}`);
                    }
                }
            }

            // Set Payment Terms nâng cao
            let paymentTermInput = header.payment_terms || header.carrier_info?.terms;
            if (paymentTermInput) {
                let termsField = poRecord.getField({ fieldId: 'terms' });
                if (termsField) {
                    let termsOptions = termsField.getSelectOptions();
                    let matchedTermId = null;

                    if (termsOptions && termsOptions.length > 0) {
                        let cleanInput = paymentTermInput.toString().replace(/\s+/g, '').toLowerCase();
                        let inputDigits = paymentTermInput.toString().replace(/\D/g, '');

                        for (let i = 0; i < termsOptions.length; i++) {
                            let optText = termsOptions[i].text;
                            let cleanOptText = optText.replace(/\s+/g, '').toLowerCase();
                            if (cleanOptText === cleanInput) {
                                matchedTermId = termsOptions[i].value;
                                break;
                            }
                        }

                        if (!matchedTermId && inputDigits !== '') {
                            for (let i = 0; i < termsOptions.length; i++) {
                                let optText = termsOptions[i].text;
                                let optDigits = optText.replace(/\D/g, '');
                                if (optDigits === inputDigits) {
                                    matchedTermId = termsOptions[i].value;
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
                        log.audit('Set Header Field', `Terms Input: "${paymentTermInput}" -> Matched Terms ID: ${matchedTermId}`);
                    } else {
                        log.error('Terms Match Failed', `Không thể khớp Payment Terms cho chuỗi: "${paymentTermInput}"`);
                    }
                }
            }

            // Set Original Customer Custom Field
            if (originalCustomerId) {
                setCustomFieldIfExists(poRecord, 'custbody_sig_po_original_customer', originalCustomerId);
                log.audit('Set Custom Field', `Original Customer (custbody_sig_po_original_customer) -> ID: ${originalCustomerId}`);
            }

            // Set Customer PO Number Custom Field
            if (header.po_number) {
                poRecord.setValue({ fieldId: 'custbody_sig_customerpo', value: header.po_number });
                log.audit('Set Custom Field', `Customer PO# (custbody_sig_customerpo) -> ${header.po_number}`);
            }

            // Set Transaction Date
            if (header.po_date) {
                let dateStr = header.po_date.substring(0, 10);
                let parts = dateStr.split('-');
                if (parts.length === 3) {
                    let poDate = new Date(parts[0], parts[1] - 1, parts[2]);
                    poRecord.setValue({ fieldId: 'trandate', value: poDate });
                    log.audit('Set Header Field', `TranDate -> ${poDate.toISOString()}`);
                }
            }

            if (termsId) {
                poRecord.setValue({ fieldId: 'terms', value: parseInt(termsId, 10) });
            }

            setCustomFieldIfExists(poRecord, 'custbody_po_type', 'MP');

            // 7. Trace Line Items
            log.audit('Step 7: Line Items Loop', `Bắt đầu duyệt ${lineItems.length} chi tiết mặt hàng`);
            for (let i = 0; i < lineItems.length; i++) {
                let line = lineItems[i];
                log.audit(`Line [${i + 1}/${lineItems.length}] Processing`, `Item SKU: ${line.item} | Qty: ${line.quantity} | Price: ${line.price_each}`);

                let itemId = getItemInternalId(line.item);
                if (!itemId) {
                    log.error(`Line [${i + 1}] Error`, `Không tìm thấy Internal ID cho SKU: ${line.item}, bỏ qua dòng này!`);
                    continue;
                }

                poRecord.selectNewLine({ sublistId: 'item' });
                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'item', value: parseInt(itemId, 10) });
                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'quantity', value: line.quantity });
                poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'rate', value: line.price_each });

                // Gán Location lên từng dòng item nếu có truyền targetLocationId từ header
                if (targetLocationId) {
                    try {
                        poRecord.setCurrentSublistValue({
                            sublistId: 'item',
                            fieldId: 'location',
                            value: targetLocationId
                        });
                        log.audit(`Line [${i + 1}] Location Set`, `Location ID: ${targetLocationId}`);
                    } catch (locErr) {
                        log.error(`Line [${i + 1}] Location Error`, `Không thể gán Location ID ${targetLocationId} trên dòng item: ${locErr.message}`);
                    }
                }

                // Gán Tax Code lên từng dòng nếu request header có truyền tax_code khác 0
                let lineTaxCode = header.tax_code;
                if (lineTaxCode !== null && lineTaxCode !== undefined && lineTaxCode !== 0) {
                    try {
                        poRecord.setCurrentSublistValue({
                            sublistId: 'item',
                            fieldId: 'taxcode',
                            value: lineTaxCode
                        });
                        log.audit(`Line [${i + 1}] Tax Code Set`, `Tax Code ID: ${lineTaxCode}`);
                    } catch (taxErr) {
                        log.error(`Line [${i + 1}] Tax Code Error`, `Không thể gán Tax Code ID ${lineTaxCode} trên dòng item: ${taxErr.message}`);
                    }
                }

                if (line.eta_date) {
                    let etaParts = line.eta_date.split('-');
                    if (etaParts.length === 3) {
                        let etaDate = new Date(etaParts[0], etaParts[1] - 1, etaParts[2]);
                        poRecord.setCurrentSublistValue({ sublistId: 'item', fieldId: 'expectedreceiptdate', value: etaDate });
                    }
                }

                poRecord.commitLine({ sublistId: 'item' });
                log.audit(`Line [${i + 1}] Success`, `Đã add thành công Item ID ${itemId} vào PO line`);
            }

            // Gán Project Code ngay trước khi save
            if (header.project_code) {
                let targetProjectCodeId = parseInt(header.project_code, 10);
                poRecord.setValue({
                    fieldId: 'cseg_sig_projcode',
                    value: targetProjectCodeId
                });

                let checkValue = poRecord.getValue({ fieldId: 'cseg_sig_projcode' });
                log.audit('VERIFY PROJECT CODE', `Giá trị vừa set xong trên record object là: ${checkValue}`);
            }

            // 8. Trace Lưu bản ghi PO
            log.audit('Step 8: Saving PO Record', 'Đang gọi poRecord.save()...');
            let poId = poRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: false
            });

            log.audit('Step 8 Success', `Tạo PO thành công với Internal ID: ${poId}`);

            let poFields = search.lookupFields({
                type: search.Type.PURCHASE_ORDER,
                id: poId,
                columns: ['tranid']
            });
            let poTranId = poFields.tranid;
            log.audit('Step 8 Document Number', `Số PO hiển thị trên hệ thống (Document Number): ${poTranId}`);

            let relativeUrl = url.resolveRecord({
                recordType: record.Type.PURCHASE_ORDER,
                recordId: poId,
                isEditMode: false
            });

            let domain = url.resolveDomain({
                hostType: url.HostType.APPLICATION
            });

            let absolutePoUrl = 'https://' + domain + relativeUrl;
            log.audit('Step 8 URL Generated', absolutePoUrl);

            return JSON.stringify({
                success: true,
                message: 'Tạo Purchase Order thành công!',
                poId: poId,
                poNumber: poTranId,
                poUrl: absolutePoUrl
            });

        } catch (e) {
            log.error('CRITICAL ERROR TRACE', `Lỗi tại: ${e.message} | Stack: ${JSON.stringify(e)}`);
            return JSON.stringify({
                success: false,
                message: 'Lỗi hệ thống: ' + e.message
            });
        }
    };

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

    function getItemInternalId(itemSku) {
        let itemId = null;
        if (itemSku) {
            let s = search.create({ type: search.Type.ITEM, filters: [['itemid', 'is', itemSku]], columns: ['internalid'] });
            s.run().each((res) => { itemId = res.getValue({ name: 'internalid' }); return false; });
        }
        return itemId;
    }

    function setCustomFieldIfExists(rec, fieldId, value) {
        try {
            rec.setValue({ fieldId: fieldId, value: value });
        } catch (err) {
            log.error('setCustomFieldIfExists Exception', `Không thể gán trường ${fieldId} với giá trị ${value}: ${err.message}`);
        }
    }

    return { post };
});