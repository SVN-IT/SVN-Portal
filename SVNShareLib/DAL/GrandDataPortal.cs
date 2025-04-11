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
    public class GrandDataPortal<T>
    {
        string tableName;
        string connectionString;
        public GrandDataPortal(string tableName, string connectionString)
        {
            this.tableName = tableName;
            this.connectionString = connectionString;
        }

        public List<T> GetListData(string sqlQuery, object param)
        {
            List<T> data;
            int timeOut = 1000;
            using (IDbConnection connection = new SqlConnection(connectionString))
            {
                data = connection.Query<T>(sqlQuery, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                return data;
            }
        }

        public T GetDataByID(int id)
        {
            T data;
            string sql = "SELECT * FROM " + tableName + " WHERE id = @id";
            var param = new { id = id };
            int timeOut = 1000;
            using (IDbConnection connection = new SqlConnection(connectionString))
            {
                data = connection.QueryFirstOrDefault<T>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                return data;
            }
        }

        public int InsertBulk(List<T> data, string sqlQuery)
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
                            var insertResult = connection.Execute(sqlQuery, data, trans, commandTimeout: timeOut);
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

        public int UpdateBulk(List<T> data, string sqlQuery)
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
                            var insertResult = connection.Execute(sqlQuery, data, trans, commandTimeout: timeOut);
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

        public BODataProcessResult CallStoredProcedure(string storedProcedure, DynamicParameters parameters)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var datas = connection.Query<object>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    if (datas != null)
                    {
                        processResult.OK = true;
                        processResult.Message = "Call SP success";
                        processResult.Content = datas;
                    }
                    else
                    {
                        processResult.Message = "Call SP fail";
                    }
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
