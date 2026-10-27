/**
 * @NApiVersion 2.1
 * @NScriptType ScheduledScript
 */
define(['N/record', 'N/search', 'N/log'], (record, search, log) => {

    const execute = (context) => {
        try {
            // 1. Tìm kiếm các Work Order đang có đánh dấu QUEUE_PENDING trong ô Memo
            let woSearch = search.create({
                type: search.Type.WORK_ORDER,
                filters: [
                    ['memo', 'startswith', 'QUEUE_PENDING']
                ],
                columns: ['internalid', 'memo', 'location', 'assemblyitem']
            });

            let results = woSearch.run().getRange({ start: 0, end: 50 }); // Xử lý tối đa 50 bản ghi mỗi lần chạy
            if (!results || results.length === 0) {
                log.audit('Queue Processor', 'Không tìm thấy Work Order nào đang chờ xử lý.');
                return;
            }

            for (let i = 0; i < results.length; i++) {
                let row = results[i];
                let woId = row.getValue({ name: 'internalid' });
                let memoText = row.getValue({ name: 'memo' });

                try {
                    log.audit('Processing WO', `Đang xử lý Work Order ID: ${woId}`);

                    // Parse lấy thông tin quantityBuilt từ chuỗi JSON trong Memo
                    let jsonStr = memoText.replace('QUEUE_PENDING: ', '');
                    let payload = JSON.parse(jsonStr);
                    let quantityBuilt = parseFloat(payload.quantityBuilt);

                    // 2. Thực hiện Transform sang Assembly Build ở Dynamic Mode
                    let buildRecord = record.transform({
                        fromType: record.Type.WORK_ORDER,
                        fromId: woId,
                        toType: record.Type.ASSEMBLY_BUILD,
                        isDynamic: true
                    });

                    buildRecord.setValue({
                        fieldId: 'quantity',
                        value: quantityBuilt
                    });

                    let locationId = buildRecord.getValue({ fieldId: 'location' });
                    let assemblyItemId = buildRecord.getValue({ fieldId: 'item' });

                    let assemblyItemName = search.lookupFields({
                        type: search.Type.ITEM,
                        id: assemblyItemId,
                        columns: ['itemid']
                    }).itemid || 'ITEM';

                    let defaultBinId = getDefaultBinId(locationId, 'Material ok');
                    let targetLotNumber = generateNextLotNumber(assemblyItemId, assemblyItemName);
                    let lotNumberId = getOrCreateInventoryNumberId(assemblyItemId, locationId, targetLotNumber);

                    // 3. COMPONENT: Phân bổ FIFO cho linh kiện trước
                    let numLines = buildRecord.getLineCount({ sublistId: 'component' });
                    for (let l = 0; l < numLines; l++) {
                        buildRecord.selectLine({ sublistId: 'component', line: l });

                        let itemId = buildRecord.getCurrentSublistValue({ sublistId: 'component', fieldId: 'item' });
                        let qtyRequired = parseFloat(buildRecord.getCurrentSublistValue({ sublistId: 'component', fieldId: 'quantity' }) || 0);

                        let subRec = null;
                        try {
                            subRec = buildRecord.getCurrentSublistSubrecord({
                                sublistId: 'component',
                                fieldId: 'componentinventorydetail'
                            });
                        } catch (subErr) { }

                        if (subRec && qtyRequired > 0) {
                            let existingLines = subRec.getLineCount({ sublistId: 'inventoryassignment' });
                            for (let j = existingLines - 1; j >= 0; j--) {
                                subRec.removeLine({ sublistId: 'inventoryassignment', line: j, ignoreRecalc: true });
                            }

                            let lotList = getAvailableLotsFIFO(itemId, locationId);
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
                        }

                        buildRecord.commitLine({ sublistId: 'component' });
                    }

                    // 4. HEADER: Cấu hình Inventory Detail cho Thành phẩm ở bước CUỐI CÙNG
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
                                value: quantityBuilt
                            });

                            assemblySubRec.commitLine({ sublistId: 'inventoryassignment' });
                        }
                    }

                    // 5. Lưu bản ghi Assembly Build
                    let savedBuildId = buildRecord.save({
                        enableSourcing: true,
                        ignoreMandatoryFields: true
                    });

                    log.audit('Assembly Build Created', `Thành công tạo Assembly Build ID: ${savedBuildId} cho WO ID: ${woId}`);

                    // 6. Sau khi xử lý xong, cập nhật lại Memo của Work Order thành trạng thái Completed để tránh bị lặp lại
                    record.submitFields({
                        type: record.Type.WORK_ORDER,
                        id: woId,
                        values: {
                            'memo': `QUEUE_COMPLETED: Assembly Build ID ${savedBuildId} - Lot: ${targetLotNumber}`
                        }
                    });

                } catch (itemErr) {
                    log.error(`Error processing WO ${woId}`, itemErr.message);
                    // Đánh dấu lỗi vào memo để dễ kiểm tra
                    record.submitFields({
                        type: record.Type.WORK_ORDER,
                        id: woId,
                        values: {
                            'memo': `QUEUE_ERROR: ${itemErr.message}`
                        }
                    });
                }
            }

        } catch (e) {
            log.error('Scheduled Script Critical Error', e.message);
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

    return { execute };
});