using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.NpgDAL
{
    public class NPG_stock_move_dataportal
    {
        string connectionString;
        public NPG_stock_move_dataportal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void ReadList()
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                // Define the SQL query
                string query = @"
                SELECT * 
                FROM public.stock_move
                WHERE write_date::date = '2025-03-20'
                ORDER BY write_date DESC";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"ID: {reader["id"]}, Write Date: {reader["write_date"]}, Name: {reader["name"]}");
                        }
                    }
                }
            }
        }
    }
}
