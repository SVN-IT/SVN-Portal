/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const get = (requestParams) => {
        try {
            const customerCode = requestParams.customerId;
            const targetCurrency = requestParams.currency ? requestParams.currency.toUpperCase() : 'USD';

            log.audit('STEP 1: API Triggered', `Khách hàng: ${customerCode} | Currency Param: ${targetCurrency}`);

            if (!customerCode) {
                return JSON.stringify({
                    success: false,
                    message: 'Vui lòng truyền tham số customerId trên URL.'
                });
            }

            // 1. Tìm Internal ID của Customer
            let internalCustomerId = null;
            search.create({
                type: search.Type.CUSTOMER,
                filters: [['entityid', 'is', customerCode]],
                columns: ['internalid']
            }).run().each((result) => {
                internalCustomerId = result.getValue({ name: 'internalid' });
                return false;
            });

            if (!internalCustomerId) {
                return JSON.stringify({
                    success: false,
                    message: `Không tìm thấy khách hàng có mã: ${customerCode}`
                });
            }

            // 2. Load bản ghi Customer
            let customerRecord = record.load({
                type: record.Type.CUSTOMER,
                id: internalCustomerId,
                isDynamic: false
            });

            let itemPricingList = [];
            let lineCount = customerRecord.getLineCount({ sublistId: 'itempricing' });

            for (let i = 0; i < lineCount; i++) {
                let itemId = customerRecord.getSublistValue({ sublistId: 'itempricing', fieldId: 'item', line: i });
                let itemName = customerRecord.getSublistText({ sublistId: 'itempricing', fieldId: 'item', line: i });
                let assignedPriceLevel = customerRecord.getSublistText({ sublistId: 'itempricing', fieldId: 'pricelevel', line: i }) || 'Base Price';
                let assignedCurrency = targetCurrency;

                // 3. Truy xuất toàn bộ ma trận giá bằng NATIVE JOIN (Sửa lỗi Formulatext)
                let pricingTiers = [];
                if (itemId) {
                    try {
                        let pricingSearch = search.create({
                            type: search.Type.ITEM,
                            filters: [['internalid', 'anyof', itemId]],
                            columns: [
                                search.createColumn({ name: 'pricelevel', join: 'pricing' }),
                                search.createColumn({ name: 'currency', join: 'pricing' }),
                                search.createColumn({ name: 'minimumquantity', join: 'pricing' }), // Sửa từ quantity thành minimumquantity
                                search.createColumn({ name: 'unitprice', join: 'pricing' })
                            ]
                        });

                        pricingSearch.run().each((res) => {
                            let pLevel = res.getText({ name: 'pricelevel', join: 'pricing' });
                            let curr = res.getText({ name: 'currency', join: 'pricing' });
                            let qty = res.getValue({ name: 'minimumquantity', join: 'pricing' });
                            let rate = res.getValue({ name: 'unitprice', join: 'pricing' });

                            // Khớp đúng chuẩn giá và tiền tệ
                            if (pLevel === assignedPriceLevel && curr === assignedCurrency) {
                                pricingTiers.push({
                                    quantity: qty ? parseFloat(qty) : 0,
                                    price: rate ? parseFloat(rate) : 0
                                });
                            }
                            return true;
                        });

                        // Sắp xếp các mốc MOQ từ thấp đến cao (Ví dụ: 0, 1000, 1500, 2000)
                        pricingTiers.sort((a, b) => a.quantity - b.quantity);

                        // Fallback chỉ chạy nếu danh sách giá bị rỗng
                        if (pricingTiers.length === 0 && assignedPriceLevel === 'Base Price') {
                            let itemFields = search.lookupFields({
                                type: search.Type.ITEM,
                                id: itemId,
                                columns: ['baseprice']
                            });
                            if (itemFields && itemFields.baseprice) {
                                pricingTiers.push({
                                    quantity: 0,
                                    price: parseFloat(itemFields.baseprice)
                                });
                            }
                        }

                    } catch (errSearch) {
                        log.error(`Error resolving pricing tiers for item ${itemId}`, errSearch.message);
                    }
                }

                itemPricingList.push({
                    itemId: itemId,
                    itemName: itemName,
                    priceLevel: assignedPriceLevel,
                    currency: assignedCurrency,
                    pricingTiers: pricingTiers
                });
            }

            return JSON.stringify({
                success: true,
                customerCode: customerCode,
                currencyParam: targetCurrency,
                internalId: internalCustomerId,
                totalItems: itemPricingList.length,
                items: itemPricingList
            });

        } catch (e) {
            log.error('Main RESTlet Critical Error', e.message);
            return JSON.stringify({
                success: false,
                message: 'Lỗi hệ thống: ' + e.message
            });
        }
    };

    return { get };
});