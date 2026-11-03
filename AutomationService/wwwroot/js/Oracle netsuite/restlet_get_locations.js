/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/query', 'N/log'], function (query, log) {

    function _get(requestParams) {
        try {
            const subsidiaryId = requestParams.subsidiaryId;
            log.audit('RESTlet Triggered', 'Lấy danh sách Location cho subsidiaryId: ' + (subsidiaryId || 'ALL'));

            let sql = "";
            let params = [];

            if (subsidiaryId) {
                // Lọc location theo một Subsidiary cụ thể
                sql = `
                    SELECT 
                        id, 
                        name, 
                        subsidiary, 
                        isinactive,
                        locationtype
                    FROM 
                        location
                    WHERE 
                        subsidiary = ?
                    ORDER BY 
                        id ASC
                `;
                params = [subsidiaryId];
            } else {
                // Lấy toàn bộ location nếu không truyền tham số
                sql = `
                    SELECT 
                        id, 
                        name, 
                        subsidiary, 
                        isinactive,
                        locationtype
                    FROM 
                        location
                    ORDER BY 
                        id ASC
                `;
            }

            let result = query.runSuiteQL({ query: sql, params: params }).asMappedResults();

            return JSON.stringify({
                success: true,
                subsidiaryId: subsidiaryId || 'ALL',
                total: result.length,
                locations: result
            });

        } catch (e) {
            log.error('Location RESTlet Fatal Error', e.message);
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