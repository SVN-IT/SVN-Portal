using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SVNShareLib.DTO;
using Dapper;

namespace SVNShareLib.DAL
{
    public class stock_move_line_consume_relĐataPortal
    {
        string connectionString;
        public stock_move_line_consume_relĐataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public List<stock_move_line_consume_relUI> GetDataByConsumeLineID(int consume_line_id)
        {
            string sql = "SELECT * FROM  SVN_stock_move_line_consume_rel WHERE consume_line_id = @consume_line_id";
            var param = new { consume_line_id = consume_line_id };
            int timeOut = 1000;
            using (IDbConnection connection = new SqlConnection(connectionString))
            {
                var data = connection.Query<stock_move_line_consume_relUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                return data.ToList();
            }
        }

        public stock_move_line_consume_relUI GetDataByConsumeLineIDAndProduceLineID(int consume_line_id, int produce_line_id)
        {
            string sql = "SELECT * FROM  SVN_stock_move_line_consume_rel WHERE consume_line_id = @consume_line_id AND produce_line_id = @produce_line_id";
            var param = new { consume_line_id = consume_line_id, produce_line_id = produce_line_id };
            int timeOut = 1000;
            using (IDbConnection connection = new SqlConnection(connectionString))
            {
                var data = connection.QueryFirstOrDefault<stock_move_line_consume_relUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                return data;
            }
        }
    }
}
