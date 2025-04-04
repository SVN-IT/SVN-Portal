using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SVNShareLib.DAL
{
    public class DatabaseDataPortal
    {
        string connectionString;
        string tableName;
        public DatabaseDataPortal(string connectionString, string tableName)
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
        }

        /// <summary>
        /// Get List Data frmo DB
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public List<object[]> GetListData(string query, DynamicParameters parameters)
        {
            try
            {
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.Query<object[]>(query, parameters, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get Single Data from DB
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public object[] GetSingleData(string query, DynamicParameters parameters)
        {
            try
            {
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.QueryFirstOrDefault<object[]>(query, parameters, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Insert hoặc update dữ liệu vào DB
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool ExecuteData(string query, DynamicParameters parameters)
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
                            var executeResult = connection.Execute(query, parameters, trans, commandTimeout: timeOut);
                            if (executeResult <= 0)
                            {
                                trans.Rollback();
                                return false;
                            }
                            else
                            {
                                trans.Commit();
                                return true;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
