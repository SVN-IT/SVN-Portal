using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Threading;

namespace SVNShareLib.DAL
{
    public class mrp_productionDataPortal
    {
        string connectionString;
        public mrp_productionDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public mrp_productionUI GetDataByID(int id)
        {
            try
            {
                mrp_productionUI data = new mrp_productionUI();
                string sql = "SELECT * FROM SVN_mrp_production_1 WHERE id = @id";
                var param = new { id = id };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.QueryFirstOrDefault<mrp_productionUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public int InsertBulk(List<mrp_productionUI> mrp_ProductionUIs)
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    using (var trans = connection.BeginTransaction())
                    {
                        try
                        {
                            var insertResult = connection.Execute("INSERT INTO [dbo].[SVN_mrp_production_1]([id],[message_main_attachment_id],[backorder_sequence],[product_id],[product_uom_id],[lot_producing_id],[picking_type_id],[location_src_id],[location_dest_id],[bom_id],[user_id],[company_id],[procurement_group_id],[orderpoint_id],[production_location_id],[create_uid],[write_uid],[origin_message_id],[origin_references],[name],[priority],[origin],[state],[reservation_state],[product_description_variants],[consumption],[product_qty],[qty_producing],[propagate_cancel],[is_locked],[is_planned],[allow_workorder_dependencies],[date_planned_start],[date_planned_finished],[date_deadline],[date_start],[date_finished],[create_date],[write_date],[product_uom_qty],[analytic_account_id],[extra_cost],[x_Svn_customer_SN])VALUES(@id,@message_main_attachment_id,@backorder_sequence,@product_id,@product_uom_id,@lot_producing_id,@picking_type_id,@location_src_id,@location_dest_id,@bom_id,@user_id,@company_id,@procurement_group_id,@orderpoint_id,@production_location_id,@create_uid,@write_uid,@origin_message_id,@origin_references,@name,@priority,@origin,@state,@reservation_state,@product_description_variants,@consumption,@product_qty,@qty_producing,@propagate_cancel,@is_locked,@is_planned,@allow_workorder_dependencies,@date_planned_start,@date_planned_finished,@date_deadline,@date_start,@date_finished,@create_date,@write_date,@product_uom_qty,@analytic_account_id,@extra_cost,@x_Svn_customer_SN)", mrp_ProductionUIs, trans, commandTimeout: timeOut);
                            if (insertResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }
                            else
                            {
                                trans.Commit();
                                return insertResult;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return -1;
                        }
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        public BODataProcessResult CallUpdateResutl()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    string storedProcedure = "SVN_Update_result_Viindoo_WC_Astro"; //SVN_Update_result_Viindoo_WC_Astro SVN_Update_result_Viindoo
                    DynamicParameters parameters = new DynamicParameters();
                    var datas = connection.Query<object[]>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    if(datas != null)
                    {
                        processResult.OK = true;
                        processResult.Message = "Call SP success";
                    }
                    else
                    {
                        processResult.Message = "Call SP fail";
                    }
                }
            }
            catch(Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
