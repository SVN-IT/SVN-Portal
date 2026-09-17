/**
 * @NApiVersion 2.1
 * @NScriptType UserEventScript
 */
define(['N/https', 'N/record', 'N/log'], (https, record, log) => {
    const afterSubmit = (scriptContext) => {
        // Chỉ chạy khi Work Order mới được tạo (CREATE)
        if (scriptContext.type !== scriptContext.UserEventType.CREATE) return;

        try {
            const newRec = scriptContext.newRecord;

            // Lấy các thông tin cần thiết từ Work Order vừa tạo
            const payload = {
                internalId: newRec.id,
                tranId: newRec.getValue({ fieldId: 'tranid' }),
                itemId: newRec.getValue({ fieldId: 'assemblyitem' }),
                quantity: newRec.getValue({ fieldId: 'quantity' }),
                startDate: newRec.getText({ fieldId: 'startdate' }),
                endDate: newRec.getText({ fieldId: 'enddate' }),
                location: newRec.getText({ fieldId: 'location' })
            };

            // Thay URL API endpoint C# của bạn vào đây
            const apiUrl = 'https://your-domain.com/api/netsuite/workorder';

            const response = https.post({
                url: apiUrl,
                body: JSON.stringify(payload),
                headers: { 'Content-Type': 'application/json' }
            });

            log.debug('Sync Result', response.body);
        } catch (e) {
            log.error('Error Syncing WO to SQL', e);
        }
    };

    return { afterSubmit };
});