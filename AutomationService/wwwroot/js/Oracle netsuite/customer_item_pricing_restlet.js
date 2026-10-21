/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const get = (requestParams) => {
        try {
            const customerCode = requestParams.customerId;
            const targetCurrency = requestParams.currency ? requestParams.currency.toUpperCase() : null; // Nhận đồng tiền truyền vào (VD: USD, CNY, VND...)

            log.audit('STEP 1: API Triggered', `Khách hàng: ${customerCode} | Currency: ${targetCurrency || 'Tất cả'}`);

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

            log.audit('STEP 2: Search Customer', `Internal ID tìm thấy: ${internalCustomerId}`);

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
            log.audit('STEP 3: Customer Loaded', `Tổng số dòng trong itempricing: ${lineCount}`);

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
                let customerPriceLevel = customerRecord.getSublistText({
                    sublistId: 'itempricing',
                    fieldId: 'pricelevel',
                    line: i
                });
                let customerCurrency = customerRecord.getSublistText({
                    sublistId: 'itempricing',
                    fieldId: 'currency',
                    line: i
                });
                let customerPrice = customerRecord.getSublistValue({
                    sublistId: 'itempricing',
                    fieldId: 'price',
                    line: i
                });

                // 3. Lấy ma trận giá và lọc theo đồng tiền động
                let itemPricingMatrix = [];
                if (itemId) {
                    try {
                        let itemFields = search.lookupFields({
                            type: search.Type.ITEM,
                            id: itemId,
                            columns: ['baseprice']
                        });

                        // Nếu người dùng không truyền currency hoặc truyền khớp với điều kiện
                        if (itemFields && itemFields.baseprice) {
                            // Mặc định Base Price thường gắn với đồng tiền cơ sở hoặc theo yêu cầu lọc
                            itemPricingMatrix.push({
                                priceLevel: 'Base Price',
                                currency: targetCurrency || 'USD',
                                qty0Price: itemFields.baseprice
                            });
                        }

                        // Lấy thêm các mức giá tiêu chuẩn khác từ item
                        let itemLookupMatrix = search.create({
                            type: search.Type.ITEM,
                            filters: [['internalid', 'anyof', itemId]],
                            columns: ['price1', 'price2', 'price3', 'price4', 'price5']
                        });

                        itemLookupMatrix.run().each((res) => {
                            for (let p = 1; p <= 5; p++) {
                                let val = res.getValue(`price${p}`);
                                if (val) {
                                    itemPricingMatrix.push({
                                        priceLevel: `Price Level ${p}`,
                                        currency: targetCurrency || 'USD',
                                        qty0Price: val
                                    });
                                }
                            }
                            return false;
                        });

                    } catch (searchErr) {
                        log.error(`Error searching pricing for item ${itemId}`, searchErr.message);
                    }
                }

                itemPricingList.push({
                    itemId: itemId,
                    itemName: itemName,
                    assignedPriceLevel: customerPriceLevel,
                    assignedCurrency: customerCurrency,
                    assignedPrice: customerPrice,
                    itemPricingMatrix: itemPricingMatrix
                });
            }

            return JSON.stringify({
                success: true,
                customerCode: customerCode,
                currencyFilter: targetCurrency || 'ALL',
                internalId: internalCustomerId,
                totalItems: lineCount,
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