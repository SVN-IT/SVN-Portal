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

            // 1. Transform sang Assembly Build ở chế độ Dynamic Mode
            const buildRecord = record.transform({
                fromType: record.Type.WORK_ORDER,
                fromId: internalId,
                toType: record.Type.ASSEMBLY_BUILD,
                isDynamic: true
            });

            // Set số lượng build ngay từ đầu
            buildRecord.setValue({
                fieldId: 'quantity',
                value: parseFloat(quantityBuilt)
            });

            const locationId = buildRecord.getValue({ fieldId: 'location' });
            const assemblyItemId = buildRecord.getValue({ fieldId: 'item' });

            const assemblyItemName = search.lookupFields({
                type: search.Type.ITEM,
                id: assemblyItemId,
                columns: ['itemid']
            }).itemid || 'ITEM';

            const defaultBinId = getDefaultBinId(locationId, 'Material ok');
            const targetLotNumber = generateNextLotNumber(assemblyItemId, assemblyItemName);
            let lotNumberId = getOrCreateInventoryNumberId(assemblyItemId, locationId, targetLotNumber);

            // 2. COMPONENT: Xử lý phân bổ FIFO cho linh kiện trước
            const numLines = buildRecord.getLineCount({ sublistId: 'component' });
            for (let i = 0; i < numLines; i++) {
                buildRecord.selectLine({ sublistId: 'component', line: i });

                const itemId = buildRecord.getCurrentSublistValue({ sublistId: 'component', fieldId: 'item' });
                const qtyRequired = parseFloat(buildRecord.getCurrentSublistValue({ sublistId: 'component', fieldId: 'quantity' }) || 0);

                let subRec = null;
                try {
                    subRec = buildRecord.getCurrentSublistSubrecord({
                        sublistId: 'component',
                        fieldId: 'componentinventorydetail'
                    });
                } catch (subErr) {
                    log.debug('Component no subrecord', subErr.message);
                }

                if (subRec && qtyRequired > 0) {
                    let existingLines = subRec.getLineCount({ sublistId: 'inventoryassignment' });
                    for (let j = existingLines - 1; j >= 0; j--) {
                        subRec.removeLine({ sublistId: 'inventoryassignment', line: j, ignoreRecalc: true });
                    }

                    const lotList = getAvailableLotsFIFO(itemId, locationId);
                    let remainingQtyToAssign = qtyRequired;

                    for (let lot of lotList) {
                        if (remainingQtyToAssign <= 0) break;

                        let assignQty = Math.min(remainingQtyToAssign, lot.available);

                        subRec.selectNewLine({ sublistId: 'inventoryassignment' });
                        subRec.setCurrentSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'issueinventorynumber',
                            value: lot.inventoryNumberInternalId
                        });

                        let targetBin = lot.binId || defaultBinId;
                        if (targetBin) {
                            subRec.setCurrentSublistValue({
                                sublistId: 'inventoryassignment',
                                fieldId: 'bin',
                                value: targetBin
                            });
                        }

                        subRec.setCurrentSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'inventorystatus',
                            value: 1
                        });

                        subRec.setCurrentSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'quantity',
                            value: assignQty
                        });

                        subRec.commitLine({ sublistId: 'inventoryassignment' });
                        remainingQtyToAssign -= assignQty;
                    }

                    subRec.commit();

                    if (remainingQtyToAssign > 0) {
                        log.error('FIFO Warning', `Không đủ tồn kho cho item ${itemId}. Thiếu: ${remainingQtyToAssign}`);
                    }
                }

                buildRecord.commitLine({ sublistId: 'component' });
            }

            // 3. HEADER: Cấu hình Inventory Detail cho Thành phẩm ở bước CUỐI CÙNG trước khi save
            if (lotNumberId) {
                let assemblySubRec = buildRecord.editSubrecord({ fieldId: 'inventorydetail' });
                if (assemblySubRec) {
                    let lineCount = assemblySubRec.getLineCount({ sublistId: 'inventoryassignment' });
                    for (let j = lineCount - 1; j >= 0; j--) {
                        assemblySubRec.removeLine({ sublistId: 'inventoryassignment', line: j, ignoreRecalc: true });
                    }

                    assemblySubRec.selectNewLine({ sublistId: 'inventoryassignment' });

                    assemblySubRec.setCurrentSublistValue({
                        sublistId: 'inventoryassignment',
                        fieldId: 'receiptinventorynumber',
                        value: lotNumberId
                    });

                    if (defaultBinId) {
                        assemblySubRec.setCurrentSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'bin',
                            value: defaultBinId
                        });
                    }

                    assemblySubRec.setCurrentSublistValue({
                        sublistId: 'inventoryassignment',
                        fieldId: 'inventorystatus',
                        value: 1
                    });

                    assemblySubRec.setCurrentSublistValue({
                        sublistId: 'inventoryassignment',
                        fieldId: 'quantity',
                        value: parseFloat(quantityBuilt)
                    });

                    assemblySubRec.commitLine({ sublistId: 'inventoryassignment' });
                    // Lưu ý: Không gọi assemblySubRec.commit() vì subrecord tự động lưu trạng thái khi commitLine hoàn tất.
                }
            }

            // 4. Lưu bản ghi Assembly Build
            const savedBuildId = buildRecord.save({
                enableSourcing: true,
                ignoreMandatoryFields: true
            });

            return {
                success: true,
                message: `Tạo Assembly Build thành công ID: ${savedBuildId} với lô mới: ${targetLotNumber}`
            };

        } catch (e) {
            let errorDetails = {
                name: e.name,
                message: e.message,
                stack: e.stack ? e.stack.join('\n') : 'No stack'
            };
            log.error('DETAILED RESTlet Error', JSON.stringify(errorDetails));
            return {
                success: false,
                message: `Lỗi NetSuite [${e.name}]: ${e.message}`
            };
        }
    };

    const getDefaultBinId = (locationId, binName) => {
        try {
            let binSearch = search.create({
                type: search.Type.BIN,
                filters: [['location', 'anyof', locationId], 'and', ['binnumber', 'is', binName]],
                columns: ['internalId']
            });
            let results = binSearch.run().getRange({ start: 0, end: 1 });
            if (results && results.length > 0) {
                return results[0].getValue({ name: 'internalId' });
            }
        } catch (e) { }
        return null;
    };

    const generateNextLotNumber = (itemId, itemCode) => {
        const now = new Date();
        const dateStr = `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(now.getDate()).padStart(2, '0')}`;
        const prefix = `${itemCode}-${dateStr}-`;
        let maxSequence = 0;

        try {
            let lotSearch = search.create({
                type: search.Type.INVENTORY_NUMBER,
                filters: [['item', 'anyof', itemId], 'and', ['inventorynumber', 'startswith', prefix]],
                columns: ['inventorynumber']
            });
            lotSearch.run().each((result) => {
                let seq = parseInt(result.getValue({ name: 'inventorynumber' }).replace(prefix, ''), 10);
                if (!isNaN(seq) && seq > maxSequence) maxSequence = seq;
                return true;
            });
        } catch (e) { }

        return `${prefix}${String(maxSequence + 1).padStart(4, '0')}`;
    };

    const getOrCreateInventoryNumberId = (itemId, locationId, lotName) => {
        try {
            let lotSearch = search.create({
                type: search.Type.INVENTORY_NUMBER,
                filters: [['item', 'anyof', itemId], 'and', ['inventorynumber', 'is', lotName]],
                columns: ['internalId']
            });
            let results = lotSearch.run().getRange({ start: 0, end: 1 });
            if (results && results.length > 0) {
                return results[0].getValue({ name: 'internalId' });
            }

            let lotRecord = record.create({ type: record.Type.INVENTORY_NUMBER, isDynamic: true });
            lotRecord.setValue({ fieldId: 'item', value: itemId });
            lotRecord.setValue({ fieldId: 'inventorynumber', value: lotName });
            lotRecord.setValue({ fieldId: 'location', value: locationId });
            return lotRecord.save();
        } catch (e) {
            return null;
        }
    };

    const getAvailableLotsFIFO = (itemId, locationId) => {
        let lotArray = [];
        try {
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
                    search.createColumn({ name: 'inventorynumber', sort: search.Sort.ASC }),
                    search.createColumn({ name: 'available' }),
                    search.createColumn({ name: 'binnumber' })
                ]
            });

            invDetailSearch.run().each((result) => {
                lotArray.push({
                    inventoryNumberInternalId: result.getValue({ name: 'inventorynumber' }),
                    available: parseFloat(result.getValue({ name: 'available' }) || 0),
                    binId: result.getValue({ name: 'binnumber' })
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