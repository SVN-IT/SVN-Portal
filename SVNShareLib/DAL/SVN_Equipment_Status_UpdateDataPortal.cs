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
    public class SVN_Equipment_Status_UpdateDataPortal
    {
        string connectionString;
        public SVN_Equipment_Status_UpdateDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public SVN_Equipment_StatusUI GetEquipmentStatusByOeration(string operation, DateTime datetime)
        {
            SVN_Equipment_StatusUI dataUI = new SVN_Equipment_StatusUI();
            try
            {
                string sql = "SELECT * FROM SVN_Equipment_Status_Update where Operation = @operation AND Datetime = @datetime";
                var param = new { operation = operation, datetime = datetime };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.QueryFirstOrDefault<SVN_Equipment_StatusUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_Calc_Run_Duration_UI>> GetCalDuration(string date, string operation, string storedProceduce = "Calc_Run_Duration")
        {
            List<SVN_Calc_Run_Duration_UI> dataUI = new List<SVN_Calc_Run_Duration_UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    //Gọi thủ tục tính toán kết quả theo tarhet
                    string storedProcedure = storedProceduce;
                    DynamicParameters parameters = new DynamicParameters();
                    parameters.Add("WorkDate", date);
                    parameters.Add("Operation", operation);

                    var datas = await conn.QueryAsync<SVN_Calc_Run_Duration_UI>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    return datas.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Ghét thời gian để đếm ngược
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="fromTime"></param>
        /// <returns></returns>
        public async Task<SVN_Equipment_Status_Update_Detail_UI> GetEquipmentStatusUpdateDetail(string operation)
        {
            SVN_Equipment_Status_Update_Detail_UI dataUI = new SVN_Equipment_Status_Update_Detail_UI();
            int timeOut = 1000;
            try
            {
                string sql = "select TOP(1) * from SVN_Equipment_Status_Update_Detail where Operation = @operation order by FromTime desc";
                var param = new { operation = operation };
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = await connection.QueryFirstOrDefaultAsync<SVN_Equipment_Status_Update_Detail_UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<SVN_Downtime_InfoUI> GetDowntimeInfo(string operation)
        {
            SVN_Downtime_InfoUI dataUI = new SVN_Downtime_InfoUI();
            int timeOut = 1000;
            try
            {
                string sql = "select TOP(1) * from SVN_Downtime_Info where Operation = @operation order by Datetime desc";
                var param = new { operation = operation };
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = await connection.QueryFirstOrDefaultAsync<SVN_Downtime_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_Top_Downtime_UI>> GetTopDowntimeByDate(string date)
        {
            int timeOut = 1000;
            try
            {
                string sql = @"
WITH StopRuns AS (
    SELECT 
        s.Id AS StopId,
        s.Code,
        ISNULL(NULLIF(s.Operation, ''), s.Code) AS Operation,
        s.[ISS-Code] AS IssCode,
        ISNULL(r.reason_name, N'Chưa xác định') AS ErrorName,
        s.Datetime AS StopTime,
        s.EstimateTime,
        s.Description,
        (
            SELECT MIN(r2.Datetime)
            FROM dbo.SVN_Downtime_Info r2
            WHERE (r2.Code = s.Code OR (s.Code IS NULL AND r2.Operation = s.Operation))
              AND r2.State = 'Run'
              AND r2.Datetime > s.Datetime
              AND CAST(r2.Datetime AS DATE) = CAST(s.Datetime AS DATE)
        ) AS NextRunTime
    FROM dbo.SVN_Downtime_Info s
    LEFT JOIN dbo.SVN_Downtime_Reason r ON s.[ISS-Code] = r.reason_code
    WHERE s.State <> 'Run'
      AND CAST(s.Datetime AS DATE) = @WorkDate
)
SELECT 
    Operation,
    Code,
    IssCode,
    ErrorName,
    COUNT(*) AS Occurrences,
    SUM(
        CASE 
            WHEN NextRunTime IS NOT NULL AND NextRunTime > StopTime 
                THEN DATEDIFF(SECOND, StopTime, NextRunTime) / 60.0
            WHEN NextRunTime IS NULL AND @WorkDate = CAST(GETDATE() AS DATE) AND GETDATE() > StopTime
                THEN DATEDIFF(SECOND, StopTime, GETDATE()) / 60.0
            WHEN NextRunTime IS NULL AND Try_Convert(time, EstimateTime) IS NOT NULL
                THEN DATEDIFF(SECOND, StopTime, DATEADD(SECOND, DATEDIFF(SECOND, 0, Try_Convert(time, EstimateTime)), CAST(@WorkDate AS DATETIME))) / 60.0
            ELSE 0 
        END
    ) AS TotalMinutes
FROM StopRuns
WHERE Operation IS NOT NULL AND Operation <> ''
GROUP BY Operation, Code, IssCode, ErrorName
ORDER BY TotalMinutes DESC";

                var param = new { WorkDate = date };
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = await connection.QueryAsync<SVN_Top_Downtime_UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data.ToList();
                }
            }
            catch
            {
                return new List<SVN_Top_Downtime_UI>();
            }
        }
    }
}
