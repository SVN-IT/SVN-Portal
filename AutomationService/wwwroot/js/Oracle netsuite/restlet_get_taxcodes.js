/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/search', 'N/log'], function (search, log) {

    function _get(requestParams) {
        try {
            log.audit('TAX CODE API TRIGGERED', 'Đang lấy danh sách Tax Code bằng N/search');

            let taxSearch = search.create({
                type: search.Type.SALES_TAX_ITEM,
                columns: [
                    'itemid',
                    'rate',
                    'country',
                    'isinactive'
                ]
            });

            let results = [];
            taxSearch.run().each(function (result) {
                results.push({
                    id: result.id,
                    itemid: result.getValue({ name: 'itemid' }),
                    rate: result.getValue({ name: 'rate' }),
                    country: result.getValue({ name: 'country' }),
                    isinactive: result.getValue({ name: 'isinactive' })
                });
                return true;
            });

            log.audit('SEARCH COMPLETED', 'Tổng số tax code tìm thấy: ' + results.length);

            let responseBody = JSON.stringify({
                success: true,
                total: results.length,
                taxCodes: results
            });

            log.audit('RESPONSE_JSON', responseBody);

            return responseBody;

        } catch (e) {
            log.error('TAX CODE RESTLET ERROR', e.message);
            return JSON.stringify({
                success: false,
                message: 'Lỗi hệ thống: ' + e.message
            });
        }
    }

    return {
        get: _get
    };
});