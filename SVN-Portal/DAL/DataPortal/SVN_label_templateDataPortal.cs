using PrinterServices.Objects;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using SVN_Portal.DAL.DTO;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_label_templateDataPortal
    {
        string connectionString;
        public SVN_label_templateDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public async Task<SVN_label_templateUI> ReadByID(string partNumber, string type, string operation, string tableName = "SVN_label_template")
        {
            SVN_label_templateUI dataUI = new SVN_label_templateUI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " Where partNumber = @partNumber AND type = @type AND operation = @operation";
                    param = new { partNumber = partNumber, type = type, operation = operation };
                    var data = await conn.QueryFirstOrDefaultAsync<SVN_label_templateUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data;
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        // ===== CRUD cho màn hình quản lý mẫu tem (/twist/admin/label-template) =====
        // Các hàm dưới đây ném exception để controller xử lý (VD: trùng UNIQUE partNumber + type + operation)

        const string CrudTable = "SVN_label_template";
        const int CrudTimeout = 30;

        // Danh sách: chỉ lấy 80 ký tự đầu của zplData để xem trước
        public async Task<List<SVN_label_templateUI>> GetAll()
        {
            using (IDbConnection conn = new SqlConnection(connectionString))
            {
                string sql = "SELECT id, partNumber, LEFT(zplData, 80) AS zplData, [size], [type], operation, createdAt, updatedAt " +
                             "FROM " + CrudTable + " ORDER BY partNumber, [type], operation";
                var data = await conn.QueryAsync<SVN_label_templateUI>(sql, commandTimeout: CrudTimeout, commandType: CommandType.Text);
                return data.ToList();
            }
        }

        public async Task<SVN_label_templateUI> GetById(int id)
        {
            using (IDbConnection conn = new SqlConnection(connectionString))
            {
                string sql = "SELECT id, partNumber, zplData, [size], [type], operation, createdAt, updatedAt " +
                             "FROM " + CrudTable + " WHERE id = @id";
                return await conn.QueryFirstOrDefaultAsync<SVN_label_templateUI>(sql, new { id }, commandTimeout: CrudTimeout, commandType: CommandType.Text);
            }
        }

        // Trả về id vừa tạo, createdAt do DB tự gán (DEFAULT GETDATE())
        public async Task<int> Create(SVN_label_templateUI data)
        {
            using (IDbConnection conn = new SqlConnection(connectionString))
            {
                string sql = "INSERT INTO " + CrudTable + " (partNumber, zplData, [size], [type], operation) " +
                             "VALUES (@partNumber, @zplData, @size, @type, @operation); " +
                             "SELECT CAST(SCOPE_IDENTITY() AS int);";
                return await conn.ExecuteScalarAsync<int>(sql, new { data.partNumber, data.zplData, data.size, data.type, data.operation }, commandTimeout: CrudTimeout, commandType: CommandType.Text);
            }
        }

        // Trả về số dòng bị ảnh hưởng (0 = không tìm thấy id)
        public async Task<int> Update(SVN_label_templateUI data)
        {
            using (IDbConnection conn = new SqlConnection(connectionString))
            {
                string sql = "UPDATE " + CrudTable + " SET partNumber = @partNumber, zplData = @zplData, [size] = @size, " +
                             "[type] = @type, operation = @operation, updatedAt = @updatedAt WHERE id = @id";
                return await conn.ExecuteAsync(sql, new { data.id, data.partNumber, data.zplData, data.size, data.type, data.operation, data.updatedAt }, commandTimeout: CrudTimeout, commandType: CommandType.Text);
            }
        }

        public async Task<int> Delete(int id)
        {
            using (IDbConnection conn = new SqlConnection(connectionString))
            {
                string sql = "DELETE FROM " + CrudTable + " WHERE id = @id";
                return await conn.ExecuteAsync(sql, new { id }, commandTimeout: CrudTimeout, commandType: CommandType.Text);
            }
        }
    }
}
