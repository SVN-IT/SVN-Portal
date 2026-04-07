using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace SVNShareLib.DAL
{
    public class mrp_bom_newDataPortal
    {
        string connectionString;
        public mrp_bom_newDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public mrp_bomUI GetDataByProductTemplateID(int product_tmpl_id)
        {
            try
            {
                mrp_bomUI data = new mrp_bomUI();
                string sql = "SELECT * FROM SVN_mrp_bom_new WHERE product_tmpl_id = @product_tmpl_id";
                var param = new { product_tmpl_id = product_tmpl_id };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.QueryFirstOrDefault<mrp_bomUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public int Insert(mrp_bomUI model)
        {
            try
            {
                string sql = @"
                    INSERT INTO SVN_mrp_bom_new
                    (
                        id,
                        message_main_attachment_id,
                        product_tmpl_id,
                        product_id,
                        product_uom_id,
                        sequence,
                        picking_type_id,
                        company_id,
                        create_uid,
                        write_uid,
                        origin_message_id,
                        origin_references,
                        code,
                        type,
                        ready_to_produce,
                        consumption,
                        product_qty,
                        active,
                        allow_operation_dependencies,
                        create_date,
                        write_date,
                        version,
                        previous_bom_id
                    )
                    VALUES
                    (
                        @id,
                        @message_main_attachment_id,
                        @product_tmpl_id,
                        @product_id,
                        @product_uom_id,
                        @sequence,
                        @picking_type_id,
                        @company_id,
                        @create_uid,
                        @write_uid,
                        @origin_message_id,
                        @origin_references,
                        @code,
                        @type,
                        @ready_to_produce,
                        @consumption,
                        @product_qty,
                        @active,
                        @allow_operation_dependencies,
                        @create_date,
                        @write_date,
                        @version,
                        @previous_bom_id
                    );
                    SELECT CAST(SCOPE_IDENTITY() as int);
                    ";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.ExecuteScalar<int>(sql, model);
                }
            }
            catch
            {
                return 0;
            }
        }

        public bool Update(mrp_bomUI model)
        {
            try
            {
                string sql = @"
                    UPDATE SVN_mrp_bom_new SET
                        message_main_attachment_id = @message_main_attachment_id,
                        product_tmpl_id = @product_tmpl_id,
                        product_id = @product_id,
                        product_uom_id = @product_uom_id,
                        sequence = @sequence,
                        picking_type_id = @picking_type_id,
                        company_id = @company_id,
                        write_uid = @write_uid,
                        origin_message_id = @origin_message_id,
                        origin_references = @origin_references,
                        code = @code,
                        type = @type,
                        ready_to_produce = @ready_to_produce,
                        consumption = @consumption,
                        product_qty = @product_qty,
                        active = @active,
                        allow_operation_dependencies = @allow_operation_dependencies,
                        write_date = @write_date,
                        version = @version,
                        previous_bom_id = @previous_bom_id
                    WHERE id = @id
                    ";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    int rows = connection.Execute(sql, model);
                    return rows > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool Delete(int id)
        {
            try
            {
                string sql = "DELETE FROM SVN_mrp_bom_new WHERE id = @id";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    int rows = connection.Execute(sql, new { id });
                    return rows > 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
