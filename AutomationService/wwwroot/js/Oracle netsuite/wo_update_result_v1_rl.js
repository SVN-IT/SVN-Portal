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

            // 1. Dùng record.transform với chế độ Standard (isDynamic: false) để an toàn tuyệt đối với sublist static
            const buildRecord = record.transform({
                fromType: record.Type.WORK_ORDER,
                fromId: internalId,
                toType: record.Type.ASSEMBLY_BUILD,
                isDynamic: false
            });

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

            // 2. Cấu hình Inventory Detail cho Thành phẩm (Header)
            if (lotNumberId) {
                try {
                    let assemblySubRec = buildRecord.getSubrecord({ fieldId: 'inventorydetail' });
                    if (assemblySubRec) {
                        let lineCount = assemblySubRec.getLineCount({ sublistId: 'inventoryassignment' });
                        for (let l = lineCount - 1; l >= 0; l--) {
                            assemblySubRec.removeLine({ sublistId: 'inventoryassignment', line: l });
                        }

                        assemblySubRec.insertLine({ sublistId: 'inventoryassignment', line: 0 });
                        assemblySubRec.setSublistValue({ sublistId: 'inventoryassignment', fieldId: 'receiptinventorynumber', line: 0, value: lotNumberId });
                        if (defaultBinId) {
                            assemblySubRec.setSublistValue({ sublistId: 'inventoryassignment', fieldId: 'bin', line: 0, value: defaultBinId });
                        }
                        assemblySubRec.setSublistValue({ sublistId: 'inventoryassignment', fieldId: 'inventorystatus', line: 0, value: 1 });
                        assemblySubRec.setSublistValue({ sublistId: 'inventoryassignment', fieldId: 'quantity', line: 0, value: parseFloat(quantityBuilt) });
                    }
                } catch (err) {
                    log.error('Header Detail Error', err.message);
                }
            }

            // 3. Duyệt qua từng dòng component có sẵn bằng Standard Mode (dùng line index)
            const numLines = buildRecord.getLineCount({ sublistId: 'component' });
            for (let i = 0; i < numLines; i++) {
                const itemId = buildRecord.getSublistValue({ sublistId: 'component', fieldId: 'item', line: i });
                const qtyRequired = parseFloat(buildRecord.getSublistValue({ sublistId: 'component', fieldId: 'quantity', line: i }) || 0);

                let subRec = null;
                try {
                    subRec = buildRecord.getSublistSubrecord({
                        sublistId: 'component',
                        fieldId: 'componentinventorydetail',
                        line: i
                    });
                } catch (subErr) {
                    log.debug('Component no subrecord', subErr.message);
                }

                if (subRec && qtyRequired > 0) {
                    // Xóa các dòng assignment cũ mặc định của NetSuite trên dòng subrecord này
                    let existingLines = subRec.getLineCount({ sublistId: 'inventoryassignment' });
                    for (let j = existingLines - 1; j >= 0; j--) {
                        subRec.removeLine({ sublistId: 'inventoryassignment', line: j });
                    }

                    const lotList = getAvailableLotsFIFO(itemId, locationId);
                    let remainingQtyToAssign = qtyRequired;
                    let assignLineIndex = 0;

                    for (let lot of lotList) {
                        if (remainingQtyToAssign <= 0) break;

                        let assignQty = Math.min(remainingQtyToAssign, lot.available);

                        subRec.insertLine({ sublistId: 'inventoryassignment', line: assignLineIndex });

                        subRec.setSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'issueinventorynumber',
                            line: assignLineIndex,
                            value: lot.inventoryNumberInternalId
                        });

                        let targetBin = lot.binId || defaultBinId;
                        if (targetBin) {
                            subRec.setSublistValue({
                                sublistId: 'inventoryassignment',
                                fieldId: 'bin',
                                line: assignLineIndex,
                                value: targetBin
                            });
                        }

                        subRec.setSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'inventorystatus',
                            line: assignLineIndex,
                            value: 1
                        });

                        subRec.setSublistValue({
                            sublistId: 'inventoryassignment',
                            fieldId: 'quantity',
                            line: assignLineIndex,
                            value: assignQty
                        });

                        remainingQtyToAssign -= assignQty;
                        assignLineIndex++;
                    }

                    if (remainingQtyToAssign > 0) {
                        log.error('FIFO Warning', `Không đủ tồn kho cho item ID ${itemId}. Thiếu: ${remainingQtyToAssign}`);
                    }
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
            log.error('RESTlet Error', e.message);
            return {
                success: false,
                message: 'Lỗi NetSuite: ' + e.message
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
                    search.createColumn({ name: 'bin' })
                ]
            });

            invDetailSearch.run().each((result) => {
                lotArray.push({
                    inventoryNumberInternalId: result.getValue({ name: 'inventorynumber' }),
                    available: parseFloat(result.getValue({ name: 'available' }) || 0),
                    binId: result.getValue({ name: 'bin' })
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