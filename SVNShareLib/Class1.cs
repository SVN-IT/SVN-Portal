using System;
using System.Collections.Generic;
using System.Linq;

public class Production
{
    public int Id { get; set; }
    public string State { get; set; }
    public DateTime? DateStart { get; set; }
    public DateTime? DateFinished { get; set; }
    public List<Move> MoveRawIds { get; set; } = new List<Move>();
    public List<Move> MoveFinishedIds { get; set; } = new List<Move>();
    public List<Move> MoveByproductIds { get; set; } = new List<Move>();
    public bool IsPlanned { get; set; }
    public string PickingTypeId { get; set; }
    public Location LocationSrc { get; set; }
    public Warehouse Warehouse { get; set; }

    public void Write(Dictionary<string, object> vals)
    {
        // Handle move_byproduct_ids and move_finished_ids
        if (vals.ContainsKey("move_byproduct_ids") && !vals.ContainsKey("move_finished_ids"))
        {
            var moveByproducts = vals["move_byproduct_ids"] as List<Move>;
            vals["move_finished_ids"] = MoveFinishedIds.Concat(moveByproducts).ToList();
            vals.Remove("move_byproduct_ids");
        }

        // Handle BOM precedence
        if (vals.ContainsKey("bom_id") && vals.ContainsKey("move_byproduct_ids") && vals.ContainsKey("move_finished_ids"))
        {
            var bomId = (int)vals["bom_id"];
            var bom = GetBomById(bomId); // Simulate a database call
            var bomProduct = bom.Product ?? bom.ProductTemplate.Variant;

            var moveByproducts = vals["move_byproduct_ids"] as List<Move>;
            var finishedMoves = vals["move_finished_ids"] as List<Move>;

            var joinedMoves = moveByproducts.ToList();
            foreach (var move in finishedMoves)
            {
                if (move.Command == Command.Create && move.ProductId != bomProduct.Id)
                    continue;
                joinedMoves.Add(move);
            }
            vals["move_finished_ids"] = joinedMoves;
            vals.Remove("move_byproduct_ids");
        }

        // Handle workorder_ids
        List<Production> productionsToReplan = null;
        if (vals.ContainsKey("workorder_ids"))
        {
            productionsToReplan = GetProductions().Where(p => p.IsPlanned).ToList();
        }

        // Handle move_raw_ids and move_finished_ids
        foreach (var moveType in new[] { "move_raw_ids", "move_finished_ids" })
        {
            if (!vals.ContainsKey(moveType) || new[] { "draft", "cancel", "done" }.Contains(this.State))
                continue;

            var warehouseId = LocationSrc?.Warehouse?.Id ?? 0;
            if (vals.ContainsKey("location_src_id"))
            {
                var locationSourceId = (int)vals["location_src_id"];
                var locationSource = GetLocationById(locationSourceId); // Simulate database call
                warehouseId = locationSource.Warehouse.Id;
            }

            var moveVals = vals[moveType] as List<Move>;
            foreach (var move in moveVals)
            {
                if (move.Command != Command.Create)
                    continue;

                if (move.WarehouseId == null)
                    move.WarehouseId = warehouseId;
            }
        }

        // Update picking type and assign moves
        var movesToReassign = new List<Move>();
        if (vals.ContainsKey("picking_type_id"))
        {
            var pickingTypeId = vals["picking_type_id"].ToString();
            foreach (var production in GetProductions())
            {
                if (production.State == "cancel" || production.State == "done")
                    continue;

                if (production.PickingTypeId != pickingTypeId)
                {
                    production.PickingTypeId = pickingTypeId;
                    movesToReassign.AddRange(production.MoveRawIds);
                }
            }
        }

        // Perform updates
        UpdateProductions(vals);

        // Handle post-write logic
        foreach (var production in GetProductions())
        {
            if (vals.ContainsKey("date_start"))
            {
                if (production.State == "done" || production.State == "cancel")
                    throw new Exception("You cannot move a manufacturing order once it is cancelled or done.");

                if (production.IsPlanned)
                    UnplanProduction(production);

                UpdateMoveDates(production.MoveRawIds, production.DateStart);
            }

            if (vals.ContainsKey("date_finished"))
            {
                UpdateMoveDates(production.MoveFinishedIds, production.DateFinished);
            }

            if (new[] { "move_raw_ids", "move_finished_ids", "workorder_ids" }.Any(vals.ContainsKey) && production.State != "draft")
            {
                AutoconfirmProduction(production);
                if (productionsToReplan.Contains(production))
                {
                    PlanWorkorders(production);
                }
            }

            if (production.State == "done" && (vals.ContainsKey("lot_producing_id") || vals.ContainsKey("qty_producing")))
            {
                var finishedMove = production.MoveFinishedIds.FirstOrDefault(m => m.ProductId == production.Id && m.State == "done");
                if (finishedMove != null)
                {
                    if (vals.ContainsKey("lot_producing_id"))
                        finishedMove.LotId = (int)vals["lot_producing_id"];
                    if (vals.ContainsKey("qty_producing"))
                        finishedMove.Quantity = (decimal)vals["qty_producing"];
                }
            }
        }

        // Reassign moves
        if (movesToReassign.Any())
        {
            DoUnreserveMoves(movesToReassign);
            AssignMoves(movesToReassign);
        }
    }

    // Simulate external methods for fetching/updating data
    private List<Production> GetProductions() => new List<Production>();
    private Bom GetBomById(int bomId) => new Bom();
    private Location GetLocationById(int locationId) => new Location();
    private void UpdateProductions(Dictionary<string, object> vals) { }
    private void UnplanProduction(Production production) { }
    private void UpdateMoveDates(List<Move> moves, DateTime? date) { }
    private void AutoconfirmProduction(Production production) { }
    private void PlanWorkorders(Production production) { }
    private void DoUnreserveMoves(List<Move> moves) { }
    private void AssignMoves(List<Move> moves) { }
}

public class Move
{
    public Command Command { get; set; }
    public int? WarehouseId { get; set; }
    public int ProductId { get; set; }
    public int? LotId { get; set; }
    public decimal Quantity { get; set; }
    public string State { get; set; }
}

public enum Command
{
    Create,
    Update,
    Delete
}

public class Bom
{
    public Product Product { get; set; }
    public ProductTemplate ProductTemplate { get; set; }
}

public class Product { public int Id { get; set; } }
public class ProductTemplate { public Product Variant { get; set; } }
public class Location { public Warehouse Warehouse { get; set; } }
public class Warehouse { public int Id { get; set; } }
