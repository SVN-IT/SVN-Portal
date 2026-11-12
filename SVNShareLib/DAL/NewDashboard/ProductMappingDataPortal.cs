using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SVNShareLib.DTO.NewDashboard;

namespace SVNShareLib.DAL.NewDashboard
{
    public class ProductMappingDataPortal
    {
        private readonly string connectionString;
        private readonly int timeOut = 1000;

        public ProductMappingDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<ProductMappingUI>> GetAllAsync()
        {
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = "SELECT product_id, Operation FROM ProductMapping";
                    var result = await conn.QueryAsync<ProductMappingUI>(sql, commandTimeout: timeOut);
                    return result.AsList();
                }
            }
            catch
            {
                return new List<ProductMappingUI>();
            }
        }

        public async Task<List<ProductMappingUI>> GetByOperationAsync(string operation)
        {
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = "SELECT product_id, Operation FROM ProductMapping WHERE Operation = @operation";
                    var result = await conn.QueryAsync<ProductMappingUI>(sql, new { operation }, commandTimeout: timeOut);
                    return result.AsList();
                }
            }
            catch
            {
                return new List<ProductMappingUI>();
            }
        }

        public async Task<List<ProductMappingUI>> GetByProductIdAsync(int productId)
        {
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = "SELECT product_id, Operation FROM ProductMapping WHERE product_id = @productId";
                    var result = await conn.QueryAsync<ProductMappingUI>(sql, new { productId }, commandTimeout: timeOut);
                    return result.AsList();
                }
            }
            catch
            {
                return new List<ProductMappingUI>();
            }
        }

        public async Task<int> InsertAsync(ProductMappingUI item)
        {
            if (item == null || !item.product_id.HasValue || string.IsNullOrWhiteSpace(item.Operation))
                return 0;

            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                        IF NOT EXISTS (SELECT 1 FROM ProductMapping WHERE product_id = @product_id AND Operation = @Operation)
                        BEGIN
                            INSERT INTO ProductMapping (product_id, Operation)
                            VALUES (@product_id, @Operation)
                        END";
                    return await conn.ExecuteAsync(sql, item, commandTimeout: timeOut);
                }
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> InsertBulkAsync(List<ProductMappingUI> items)
        {
            if (items == null || items.Count == 0) return 0;

            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    if (conn.State != ConnectionState.Open) conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        string sql = @"
                            IF NOT EXISTS (SELECT 1 FROM ProductMapping WHERE product_id = @product_id AND Operation = @Operation)
                            BEGIN
                                INSERT INTO ProductMapping (product_id, Operation)
                                VALUES (@product_id, @Operation)
                            END";
                        int count = await conn.ExecuteAsync(sql, items, trans, commandTimeout: timeOut);
                        trans.Commit();
                        return count;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Tra cứu thông tin sản phẩm và tên template từ ERP (SVN_product_product & SVN_product_template_1)
        /// </summary>
        public async Task<ProductInfoLookupResult> LookupProductByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            code = code.Trim();
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                        SELECT TOP 1 
                            p.id AS ProductId,
                            p.default_code AS DefaultCode,
                            p.barcode AS Barcode,
                            p.product_tmpl_id AS TemplateId,
                            t.name AS TemplateName
                        FROM SVN_product_product p
                        LEFT JOIN SVN_product_template_1 t ON p.product_tmpl_id = t.id
                        WHERE (p.default_code = @code OR p.barcode = @code)
                        ORDER BY CASE WHEN p.active = 1 THEN 0 ELSE 1 END, p.id DESC";
                    return await conn.QueryFirstOrDefaultAsync<ProductInfoLookupResult>(sql, new { code }, commandTimeout: timeOut);
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public class ProductInfoLookupResult
    {
        public int ProductId { get; set; }
        public string DefaultCode { get; set; }
        public string Barcode { get; set; }
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
    }
}
