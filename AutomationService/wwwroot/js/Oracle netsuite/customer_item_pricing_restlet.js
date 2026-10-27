/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const get = (requestParams) => {
        try {
            const customerCode = requestParams.customerId;
            const targetCurrency = requestParams.currency ? requestParams.currency.toUpperCase() : 'USD'; // Lấy currency từ param URL

            log.audit('STEP 1: API Triggered', `Khách hàng: ${customerCode} | Currency Param: ${targetCurrency}`);

            if (!customerCode) {
                return JSON.stringify({
                    success: false,
                    message: 'Vui lòng truyền tham số customerId trên URL.'
                });
            }

            // 1. Tìm Internal ID của Customer dựa vào entityid
            let internalCustomerId = null;
            let customerSearch = search.create({
                type: search.Type.CUSTOMER,
                filters: [['entityid', 'is', customerCode]],
                columns: ['internalid']
            });

            customerSearch.run().each((result) => {
                internalCustomerId = result.getValue({ name: 'internalid' });
                return false;
            });

            if (!internalCustomerId) {
                return JSON.stringify({
                    success: false,
                    message: `Không tìm thấy khách hàng có mã: ${customerCode}`
                });
            }

            // 2. Load bản ghi Customer để đọc sublist itempricing
            let customerRecord = record.load({
                type: record.Type.CUSTOMER,
                id: internalCustomerId,
                isDynamic: false
            });

            let itemPricingList = [];
            let lineCount = customerRecord.getLineCount({ sublistId: 'itempricing' });

            for (let i = 0; i < lineCount; i++) {
                let itemId = customerRecord.getSublistValue({
                    sublistId: 'itempricing',
                    fieldId: 'item',
                    line: i
                });
                let itemName = customerRecord.getSublistText({
                    sublistId: 'itempricing',
                    fieldId: 'item',
                    line: i
                });

                // Lấy Price Level đã được set riêng cho khách hàng trên dòng này
                let assignedPriceLevel = customerRecord.getSublistText({
                    sublistId: 'itempricing',
                    fieldId: 'pricelevel',
                    line: i
                }) || 'Base Price'; // Mặc định là Base Price nếu trống

                let assignedCurrency = targetCurrency;

                // 3. Quét toàn bộ ma trận giá của Item để lấy tất cả các bậc MOQ ứng với pricelevel và currency
                let pricingTiers = [];
                if (itemId) {
                    try {
                        let pricingSearch = search.create({
                            type: search.Type.ITEM,
                            filters: [['internalid', 'anyof', itemId]],
                            columns: [
                                search.createColumn({ name: 'formulatext', formula: '{pricing.pricelevel}' }),
                                search.createColumn({ name: 'formulatext', formula: '{pricing.currency}' }),
                                search.createColumn({ name: 'formulatext', formula: '{pricing.quantity}' }),
                                search.createColumn({ name: 'formulatext', formula: '{pricing.unitprice}' })
                            ]
                        });

                        pricingSearch.run().each((res) => {
                            let pLevel = res.getValue({ name: 'formulatext', formula: '{pricing.pricelevel}' });
                            let curr = res.getValue({ name: 'formulatext', formula: '{pricing.currency}' });
                            let qty = res.getValue({ name: 'formulatext', formula: '{pricing.quantity}' });
                            let rate = res.getValue({ name: 'formulatext', formula: '{pricing.unitprice}' });

                            // Khớp đúng Price Level của khách và Currency từ param
                            if (pLevel === assignedPriceLevel && curr === assignedCurrency) {
                                pricingTiers.push({
                                    quantity: qty ? parseFloat(qty) : 0,
                                    price: rate ? parseFloat(rate) : 0
                                });
                            }
                            return true; // Tiếp tục chạy để lấy hết các bậc số lượng khác
                        });

                        // Fallback nếu không tìm thấy qua search pricing và là Base Price (lấy giá trị baseprice mặc định cho Qty 0)
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
                    pricingTiers: pricingTiers // Trả về mảng danh sách giá theo từng mức số lượng (MOQ)
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