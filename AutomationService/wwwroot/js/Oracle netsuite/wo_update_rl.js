/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/log'], (record, log) => {

    const post = (requestBody) => {
        try {
            log.audit('RESTlet Payload Received', JSON.stringify(requestBody));

            const internalId = requestBody.internalId;
            const quantityBuilt = requestBody.quantityBuilt;

            if (!internalId) {
                return { success: false, message: 'Thiếu internalId của Work Order' };
            }

            const woRecord = record.load({
                type: record.Type.WORK_ORDER,
                id: internalId,
                isDynamic: true
            });

            if (quantityBuilt !== undefined && quantityBuilt !== null) {
                woRecord.setValue({
                    fieldId: 'quantity',
                    value: parseFloat(quantityBuilt)
                });
            }

            const savedId = woRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: true
            });

            return {
                success: true,
                message: `Cập nhật thành công Work Order ID: ${savedId}`
            };

        } catch (e) {
            log.error('RESTlet Error', e.message);
            return {
                success: false,
                message: 'Lỗi NetSuite: ' + e.message
            };
        }
    };

    return { post };
});