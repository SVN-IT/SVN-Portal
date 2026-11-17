/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 * @description SuiteScript 2.1 RESTlet for retrieving Person in charge (Employees) list from NetSuite.
 */
define(['N/query', 'N/search', 'N/log'], function (query, search, log) {

    function _get(requestParams) {
        try {
            log.audit('GET PERSON IN CHARGE TRIGGERED', 'Đang lấy danh sách Person in charge (Employees) từ NetSuite');

            let results = [];

            // Cách 1: Sử dụng N/query (SuiteQL) chạy nhanh, lấy toàn bộ danh sách không bị giới hạn cột
            try {
                let sql = `
                    SELECT 
                        id, 
                        entityid, 
                        firstname, 
                        lastname, 
                        email, 
                        title 
                    FROM 
                        employee 
                    WHERE 
                        isinactive = 'F' 
                    ORDER BY 
                        entityid ASC
                `;
                let queryResults = query.runSuiteQL({ query: sql }).asMappedResults();
                if (queryResults && queryResults.length > 0) {
                    queryResults.forEach(function (row) {
                        let empId = parseInt(row.id, 10);
                        let entityId = (row.entityid || '').trim();
                        let firstName = (row.firstname || '').trim();
                        let lastName = (row.lastname || '').trim();
                        let email = (row.email || '').trim();
                        let title = (row.title || '').trim();

                        let displayName = entityId;
                        if (!displayName && (firstName || lastName)) {
                            displayName = (firstName + ' ' + lastName).trim();
                        }

                        results.push({
                            id: empId,
                            name: displayName || ('Employee #' + empId),
                            entityId: entityId,
                            email: email,
                            title: title
                        });
                    });

                    log.audit('SUITEQL COMPLETED', 'Tổng số Employee tìm thấy qua SuiteQL: ' + results.length);
                }
            } catch (qlErr) {
                log.error('SUITEQL NOTICE', 'SuiteQL query failed, falling back to N/search: ' + qlErr.message);
            }

            // Cách 2: Dự phòng với N/search nếu SuiteQL không khả dụng
            if (results.length === 0) {
                let empSearch = search.create({
                    type: search.Type.EMPLOYEE,
                    filters: [
                        ['isinactive', 'is', 'F']
                    ],
                    columns: [
                        search.createColumn({ name: 'internalid', sort: search.Sort.ASC }),
                        search.createColumn({ name: 'entityid' }),
                        search.createColumn({ name: 'firstname' }),
                        search.createColumn({ name: 'lastname' }),
                        search.createColumn({ name: 'email' }),
                        search.createColumn({ name: 'title' })
                    ]
                });

                let pagedData = empSearch.runPaged({ pageSize: 1000 });
                pagedData.pageRanges.forEach(function (pageRange) {
                    let page = pagedData.fetch({ index: pageRange.index });
                    page.data.forEach(function (result) {
                        let id = result.getValue({ name: 'internalid' });
                        let entityId = (result.getValue({ name: 'entityid' }) || '').trim();
                        let firstName = (result.getValue({ name: 'firstname' }) || '').trim();
                        let lastName = (result.getValue({ name: 'lastname' }) || '').trim();
                        let email = (result.getValue({ name: 'email' }) || '').trim();
                        let title = (result.getValue({ name: 'title' }) || '').trim();

                        let displayName = entityId;
                        if (!displayName && (firstName || lastName)) {
                            displayName = (firstName + ' ' + lastName).trim();
                        }

                        results.push({
                            id: parseInt(id, 10),
                            name: displayName || ('Employee #' + id),
                            entityId: entityId,
                            email: email,
                            title: title
                        });
                    });
                });

                log.audit('SEARCH COMPLETED', 'Tổng số Employee tìm thấy qua N/search: ' + results.length);
            }

            log.audit('FINAL EMPLOYEE COUNT', 'Trả về tổng cộng: ' + results.length + ' nhân viên');

            let responseBody = JSON.stringify({
                success: true,
                total: results.length,
                employees: results
            });

            return responseBody;

        } catch (e) {
            log.error('PERSON IN CHARGE RESTLET ERROR', e.message);

            return JSON.stringify({
                success: false,
                message: 'Lỗi hệ thống khi lấy danh sách Person in charge: ' + e.message,
                employees: []
            });
        }
    }

    return {
        get: _get
    };
});
