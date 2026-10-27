-- ============================================================
-- Tạo bảng SVN_PrintLogTwist
-- Lưu log mỗi lần in tem tại trạm Twist
-- ============================================================

CREATE TABLE dbo.SVN_PrintLogTwist (
    id           INT IDENTITY(1,1) PRIMARY KEY,
    wo_code      NVARCHAR(100) NOT NULL,
    product_id   INT           NOT NULL,
    product_code NVARCHAR(100) NOT NULL,
    print_qty    INT           NOT NULL,
    print_time   DATETIME      NOT NULL DEFAULT GETDATE()
);

CREATE INDEX IX_SVN_PrintLogTwist_Date ON dbo.SVN_PrintLogTwist (print_time);
