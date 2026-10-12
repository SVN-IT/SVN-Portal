/**
 * @NApiVersion 2.1
 * @NScriptType Restlet
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const post = (requestBody) => {
        try {
            log.audit('RESTlet Payload Received', JSON.stringify(requestBody));

            const internalId = requestBody.internalId;
            const quantityBuilt = requestBody.quantityBuilt;

            if (!internalId) {
                return { success: false, message: 'Thiếu internalId của Work Order' };
            }

            if (quantityBuilt === undefined || quantityBuilt === null || parseFloat(quantityBuilt) <= 0) {
                return { success: false, message: 'Số lượng sản xuất (quantityBuilt) phải lớn hơn 0' };
            }

            // 1. Transform Work Order sang Assembly Build
            const buildRecord = record.transform({
                fromType: record.Type.WORK_ORDER,
                fromId: internalId,
                toType: record.Type.ASSEMBLY_BUILD,
                isDynamic: true
            });

            // 2. Gán số lượng thành phẩm cần build
            buildRecord.setValue({
                fieldId: 'quantity',
                value: parseFloat(quantityBuilt)
            });

            // Lấy kho (location) mặc định của Assembly Build để truy vấn tồn kho theo lô
            const locationId = buildRecord.getValue({ fieldId: 'location' });

            // 3. Duyệt qua từng dòng linh kiện (component)
            const numLines = buildRecord.getLineCount({ sublistId: 'component' });
            for (let i = 0; i < numLines; i++) {
                buildRecord.selectLine({ sublistId: 'component', line: i });

                const itemId = buildRecord.getCurrentSublistValue({
                    sublistId: 'component',
                    fieldId: 'item'
                });

                const qtyRequired = parseFloat(buildRecord.getCurrentSublistValue({
                    sublistId: 'component',
                    fieldId: 'quantity'
                }) || 0);

                // Kiểm tra xem dòng này có yêu cầu Inventory Detail (Lot/Serial) hay không
                const subRec = buildRecord.getCurrentSublistSubrecord({
                    sublistId: 'component',
                    fieldId: 'inventorydetail'
                });

                if (subRec && qtyRequired > 0) {
                    // Xóa các dòng phân bổ cũ (nếu có sẵn mặc định)
                    const existingLines = subRec.getLineCount({ sublistId: 'inventoryassignment' });
                    for (let j = existingLines - 1; j >= 0; j--) {
                        subRec.removeLine({ sublistId: 'inventoryassignment', line: j, ignoreRecalc: true });
                    }

                    // Truy vấn các lô còn tồn trong kho, sắp xếp theo thời gian nhập kho cũ nhất trước (FIFO)
                    const lotList = getAvailableLotsFIFO(itemId, locationId);

                    let remainingQtyToAssign = qtyRequired;

                    for (let lot of lotList) {
                        if (remainingQtyToAssign <= 0) break;

                        let assignQty = Math.min(remainingQtyToAssign, lot.available);

                        subRec.selectNewLine({ sublistId: 'inventoryassignment' });
                        subRec.setCurrentSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'issueinventorynumber',
                            value: lot.inventoryNumberId
                        });
                        subRec.setCurrentSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'quantity',
                            value: assignQty
                        });
                        subRec.commitLine({ sublistId: 'inventoryassignment' });

                        remainingQtyToAssign -= assignQty;
                    }

                    if (remainingQtyToAssign > 0) {
                        log.error('FIFO Warning', `Không đủ tồn kho theo lô cho item ID ${itemId}. Còn thiếu: ${remainingQtyToAssign}`);
                    }
                }

                buildRecord.commitLine({ sublistId: 'component' });
            }

            // 4. Lưu bản ghi Assembly Build
            const savedBuildId = buildRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: true
            });

            return {
                success: true,
                message: `Tạo Assembly Build (FIFO) ID: ${savedBuildId} thành công cho Work Order ID: ${internalId}`
            };

        } catch (e) {
            log.error('RESTlet FIFO Error', e.message);
            return {
                success: false,
                message: 'Lỗi NetSuite (FIFO): ' + e.message
            };
        }
    };

    /**
     * Hàm phụ trợ: Tìm danh sách lô hàng tồn kho sắp xếp theo FIFO (ngày nhập cũ nhất trước)
     */
    const getAvailableLotsFIFO = (itemId, locationId) => {
        let lotArray = [];
        try {
            // Sử dụng saved search hoặc search trực tiếp trên bảng inventorynumber / inventorybalance
            const invDetailSearch = search.create({
                type: search.Type.INVENTORY_BALANCE,
                filters: [
                    ['item', 'anyof', itemId],
                    'and',
                    ['location', 'anyof', locationId],
                    'and',
                    ['onhand', 'greaterthan', 0]
                ],
                columns: [
                    search.createColumn({ name: 'inventorynumber', sort: search.Sort.ASC }), // Sắp xếp theo số lô / thời gian
                    search.createColumn({ name: 'available' })
                ]
            });

            invDetailSearch.run().each((result) => {
                lotArray.push({
                    inventoryNumberId: result.getValue({ name: 'inventorynumber' }),
                    available: parseFloat(result.getValue({ name: 'available' }) || 0)
                });
                return true;
            });
        } catch (searchErr) {
            log.error('Search Lots Error', searchErr.message);
        }
        return lotArray;
    };

    return { post };
});