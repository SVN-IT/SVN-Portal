/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/search', 'N/log'], function (search, log) {

    function _get(requestParams) {
        try {
            log.audit('PROJECT CODE API TRIGGERED', 'Đang lấy danh sách Project Code từ hệ thống');

            // Do custom segment có thể có record riêng hoặc được query qua các bản ghi chứa nó, 
            // cách an toàn nhất trong N/search là quét danh sách Project Code qua loại tìm kiếm tương ứng hoặc 
            // dùng customrecord / custom segment nếu NetSuite hỗ trợ. 
            // Tuy nhiên, đối với trường hợp segment này, ta có thể dùng search trên bản ghi custom list hoặc dùng N/query nếu hỗ trợ, 
            // hoặc query trực tiếp từ bảng custom segment. 
            // Dưới đây là cách sử dụng N/search chuẩn để lấy các giá trị từ trường phân đoạn cseg_sig_projcode:

            let results = [];

            // Tìm kiếm các Project Code đang có trên hệ thống
            // Lưu ý: Tên record của custom segment thường có dạng cseg_<name> hoặc customlist/customrecord tương ứng.
            // Nếu cseg_sig_projcode là custom segment dạng list, ta có thể dùng search.create với type tương ứng hoặc query bảng segment.

            let projSearch = search.create({
                type: 'customrecord_cseg_sig_projcode', // Hoặc type phù hợp với custom segment của bạn
                columns: [
                    'internalid',
                    'name',
                    'isinactive'
                ]
            });

            projSearch.run().each(function (result) {
                results.push({
                    id: result.id,
                    name: result.getValue({ name: 'name' }),
                    isinactive: result.getValue({ name: 'isinactive' })
                });
                return true;
            });

            log.audit('SEARCH COMPLETED', 'Tổng số Project Code tìm thấy: ' + results.length);

            let responseBody = JSON.stringify({
                success: true,
                total: results.length,
                projectCodes: results
            });

            log.audit('RESPONSE_JSON', responseBody);
            return responseBody;

        } catch (e) {
            log.error('PROJECT CODE RESTLET ERROR', e.message);

            // Fallback: Nếu type customrecord không khớp, trả về thông báo lỗi chi tiết để điều chỉnh type
            return JSON.stringify({
                success: false,
                message: 'Lỗi hệ thống: ' + e.message + '. (Lưu ý: Type của cseg có thể là customrecord_cseg_sig_projcode hoặc customlist tương ứng).'
            });
        }
    }

    return {
        get: _get
    };
});