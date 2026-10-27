/**
 * @NApiVersion 2.1
 * @NScriptType Suitelet
 */
define(['N/record', 'N/log'], (record, log) => {

    const onRequest = (context) => {
        if (context.request.method !== 'POST') {
            context.response.write(JSON.stringify({ success: false, message: 'Chỉ hỗ trợ phương thức POST' }));
            return;
        }

        try {
            const requestBody = JSON.parse(context.request.body);
            const internalId = requestBody.internalId;
            const quantityBuilt = requestBody.quantityBuilt;

            if (!internalId) {
                context.response.write(JSON.stringify({ success: false, message: 'Thiếu internalId của Work Order' }));
                return;
            }

            // Load bản ghi Work Order hiện tại
            const woRecord = record.load({
                type: record.Type.WORK_ORDER,
                id: internalId,
                isDynamic: true
            });

            // Cập nhật số lượng hoàn thành hoặc trường tùy chỉnh tương ứng
            if (quantityBuilt !== undefined) {
                woRecord.setValue({
                    fieldId: 'quantity',
                    value: quantityBuilt
                });
            }

            const savedId = woRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: false
            });

            log.audit('WO Updated Successfully', `Work Order ID: ${savedId} updated with quantity: ${quantityBuilt}`);

            context.response.write(JSON.stringify({
                success: true,
                message: `Cập nhật thành công Work Order ID: ${savedId}`
            }));

        } catch (e) {
            log.error('Error Updating Work Order', e.message);
            context.response.write(JSON.stringify({
                success: false,
                message: e.message
            }));
        }
    };

    return { onRequest };
});