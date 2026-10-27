/**
 * @NApiVersion 2.1
 * @NScriptType UserEventScript
 */
define(['N/https', 'N/log', 'N/search', 'N/format'], (https, log, search, format) => {

    const afterSubmit = (scriptContext) => {
        log.audit('UE Triggered', `Event Type: ${scriptContext.type} | Record ID: ${scriptContext.newRecord.id}`);

        if (scriptContext.type !== scriptContext.UserEventType?.CREATE && scriptContext.type !== 'create') {
            return;
        }

        try {
            const newRec = scriptContext.newRecord;
            const recId = newRec.id;

            // 1. Truy vấn lại Work Order từ DB để lấy đúng Mã WO (tranid) sau khi đã sinh số
            let realTranId = '';
            try {
                const woLookup = search.lookupFields({
                    type: search.Type.WORK_ORDER,
                    id: recId,
                    columns: ['tranid']
                });
                realTranId = woLookup.tranid || '';
            } catch (e) {
                realTranId = newRec.getValue({ fieldId: 'tranid' }) || '';
            }

            // 2. Lấy mã Item (itemid / itemcode) chuẩn xác từ Assembly Item
            let realItemCode = '';
            const assemblyItemId = newRec.getValue({ fieldId: 'assemblyitem' });
            if (assemblyItemId) {
                try {
                    const itemLookup = search.lookupFields({
                        type: search.Type.ITEM,
                        id: assemblyItemId,
                        columns: ['itemid']
                    });
                    realItemCode = itemLookup.itemid || '';
                } catch (e) {
                    realItemCode = newRec.getText({ fieldId: 'assemblyitem' }) || String(assemblyItemId);
                }
            }

            // Helper format ngày tháng
            const FormatDateValue = (fieldId) => {
                const val = newRec.getValue({ fieldId: fieldId });
                if (!val) return '';
                if (val instanceof Date) {
                    return format.format({ value: val, type: format.Type.DATE });
                }
                return String(val);
            };

            // Helper lấy text Location an toàn
            const GetLocationText = () => {
                try {
                    return newRec.getText({ fieldId: 'location' }) || newRec.getValue({ fieldId: 'location' }) || '';
                } catch (e) {
                    return newRec.getValue({ fieldId: 'location' }) || '';
                }
            };

            // 3. Đóng gói Payload chứa đúng Mã WO và Mã Item
            const payloadList = [{
                internalId: parseInt(recId),
                tranId: String(realTranId),
                itemId: String(realItemCode),
                quantity: parseFloat(newRec.getValue({ fieldId: 'quantity' }) || 0),
                startDate: FormatDateValue('startdate'),
                endDate: FormatDateValue('enddate'),
                location: String(GetLocationText())
            }];

            log.debug('Payload Sending', JSON.stringify(payloadList));

            // 4. Gửi HTTP POST Request sang C# API
            const response = https.post({
                url: 'https://api.sigmaworldwide.io/api/Netsuite/ReceiveWorkOrderBatch',
                body: JSON.stringify(payloadList),
                headers: { 'Content-Type': 'application/json' }
            });

            log.audit('Sync Result Success', `WO ID: ${recId} | Response Code: ${response.code} | Body: ${response.body}`);

        } catch (e) {
            log.error('Error Syncing WO to SQL', `Details: ${e.message} | Stack: ${e.stack}`);
        }
    };

    return { afterSubmit };
});