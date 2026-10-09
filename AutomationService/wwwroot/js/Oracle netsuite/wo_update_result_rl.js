/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/log'], (record, log) => {

    const post = (requestBody) => {
        try {
            log.audit('RESTlet Payload Received', JSON.stringify(requestBody));

            const internalId = requestBody.internalId;      // Work Order Internal ID
            const quantityBuilt = requestBody.quantityBuilt; // Số lượng vừa hoàn thành

            if (!internalId) {
                return { success: false, message: 'Thiếu internalId của Work Order' };
            }

            if (quantityBuilt === undefined || quantityBuilt === null || parseFloat(quantityBuilt) <= 0) {
                return { success: false, message: 'Số lượng sản xuất (quantityBuilt) phải lớn hơn 0' };
            }

            // 1. Transform Work Order sang Assembly Build (Giao dịch ghi nhận sản xuất)
            const buildRecord = record.transform({
                fromType: record.Type.WORK_ORDER,
                fromId: internalId,
                toType: record.Type.ASSEMBLY_BUILD,
                isDynamic: true
            });

            // 2. Gán số lượng sản xuất hoàn thành
            buildRecord.setValue({
                fieldId: 'quantity',
                value: parseFloat(quantityBuilt)
            });

            // 3. Lưu giao dịch Assembly Build
            const savedBuildId = buildRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: true
            });

            // Cách kiểm tra: Mở lại Work Order trên giao diện NetSuite, cột BUILT sẽ tăng lên đúng bằng quantityBuilt
            return {
                success: true,
                message: `Tạo Assembly Build ID: ${savedBuildId} thành công cho Work Order ID: ${internalId}`
            };

        } catch (e) {
            log.error('RESTlet Build Error', e.message);
            return {
                success: false,
                message: 'Lỗi NetSuite: ' + e.message
            };
        }
    };

    return { post };
});