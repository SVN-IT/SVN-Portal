/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/query', 'N/log'], function (query, log) {

    function _get(requestParams) {
        try {
            const customerCode = requestParams.customerId;
            log.audit('RESTlet Triggered', 'Lấy subsidiary cho customer: ' + customerCode);

            let result = [];

            if (customerCode) {
                // Sử dụng câu lệnh SQL an toàn, đã lược bỏ trường không hợp lệ
                let sql = `
                    SELECT 
                        s.id, 
                        s.name, 
                        s.legalname, 
                        s.isinactive, 
                        s.parent
                    FROM 
                        subsidiary s
                    JOIN 
                        customer c ON c.subsidiary = s.id
                    WHERE 
                        c.entityid = ? OR TO_CHAR(c.id) = ?
                `;

                try {
                    result = query.runSuiteQL({ query: sql, params: [customerCode, customerCode] }).asMappedResults();
                } catch (sqlErr) {
                    log.error('SuiteQL Join Error, falling back', sqlErr.message);
                    let fallbackSql = `SELECT subsidiary FROM customer WHERE entityid = ? OR TO_CHAR(id) = ?`;
                    let custRes = query.runSuiteQL({ query: fallbackSql, params: [customerCode, customerCode] }).asMappedResults();

                    if (custRes.length > 0 && custRes[0].subsidiary) {
                        let subId = custRes[0].subsidiary;
                        let subSql = `SELECT id, name, legalname, isinactive, parent FROM subsidiary WHERE id = ?`;
                        result = query.runSuiteQL({ query: subSql, params: [subId] }).asMappedResults();
                    }
                }

            } else {
                let sql = `
                    SELECT 
                        id, 
                        name, 
                        legalname, 
                        isinactive, 
                        parent
                    FROM 
                        subsidiary
                    ORDER BY 
                        id ASC
                `;
                result = query.runSuiteQL({ query: sql }).asMappedResults();
            }

            return JSON.stringify({
                success: true,
                customerCode: customerCode || 'ALL',
                total: result.length,
                subsidiaries: result
            });

        } catch (e) {
            log.error('Subsidiary RESTlet Fatal Error', e.message);
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