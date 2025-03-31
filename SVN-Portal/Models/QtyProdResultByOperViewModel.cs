using SVNShareLib.DTO;
using System.Collections.Generic;

namespace SVN_Portal.Models
{
    /// <summary>
    /// Lớp tượng chưng cho data hiển thị của 1 oper bao gồm chart và table
    /// add thêm lớp target vào lớp này để hiển thị dữ liệu cùng 1 lúc
    /// </summary>
    public class QtyProdResultByOperViewModel
    {
        public QtyProdResultByOperViewModel()
        {
            ViewModels = new List<QtyProdResultViewModel>();
            TargetViewModels = new List<SVN_targetViewModel>();// khai báo kiểu này giup list ko bọ null
            ProductionUIs = new List<mrp_productionUI>();
        }
        public string Operation { get; set; }
        public string WC { get; set; }
        public string PDName { get; set; }
        public string QCName { get; set; }
        public int ColWidth { get; set; }
        public List<QtyProdResultViewModel> ViewModels { get; set; }
        public List<SVN_targetViewModel> TargetViewModels { get; set; } //Bởi vì dữ liệu tổng hợp sẽ là nhiều dong nên khai báo dang list
        public List<mrp_productionUI> ProductionUIs { get; set; }
        public double Est { get; set; }

        public QtyProdResultByOperViewModel GetData(string operation)
        {
            QtyProdResultByOperViewModel Model = new QtyProdResultByOperViewModel();
            Model.Operation = operation;

            if(operation == "POP")
            {
                List <QtyProdResultViewModel> viewModels = new List<QtyProdResultViewModel>() 
                { 
                    new QtyProdResultViewModel(){Time = "8h-10h", Target = 1225, Line = 1250, ManQuantity = 28},
                    new QtyProdResultViewModel(){Time = "10h10-12h", Target = 1122, Line = 1200, ManQuantity = 30},
                    new QtyProdResultViewModel(){Time = "13h-15h", Target = 1225, Line = 1100, ManQuantity = 26},
                    new QtyProdResultViewModel(){Time = "15h10-17h30", Target = 1430, Line = 0, ManQuantity = 24},
                    new QtyProdResultViewModel(){Time = "18h-20h", Target = 0, Line = 0, ManQuantity = 0},
                };
                Model.ViewModels = viewModels;
            }
            else if(operation == "eKIT")
            {
                List<QtyProdResultViewModel> viewModels = new List<QtyProdResultViewModel>()
                {
                    new QtyProdResultViewModel(){Time = "8h-10h", Target = 0, Line = 0, ManQuantity = 0},
                    new QtyProdResultViewModel(){Time = "10h10-12h", Target = 0, Line = 0, ManQuantity = 0},
                    new QtyProdResultViewModel(){Time = "13h-15h", Target = 32, Line = 30, ManQuantity = 4},
                    new QtyProdResultViewModel(){Time = "15h10-17h30", Target = 55, Line = 0, ManQuantity = 6},
                    new QtyProdResultViewModel(){Time = "18h-20h", Target = 0, Line = 0, ManQuantity = 0},
                };
                Model.ViewModels = viewModels;
            }
            else if (operation == "Solar")
            {
                List<QtyProdResultViewModel> viewModels = new List<QtyProdResultViewModel>()
                {
                    new QtyProdResultViewModel(){Time = "8h-10h", Target = 26, Line = 26, ManQuantity = 10},
                    new QtyProdResultViewModel(){Time = "10h10-12h", Target = 24, Line = 24, ManQuantity = 10},
                    new QtyProdResultViewModel(){Time = "13h-15h", Target = 26, Line = 26, ManQuantity = 10},
                    new QtyProdResultViewModel(){Time = "15h10-17h30", Target = 30, Line = 0, ManQuantity = 10},
                    new QtyProdResultViewModel(){Time = "18h-20h", Target = 26, Line = 0, ManQuantity = 10},
                };
                Model.ViewModels = viewModels;
            }
            else if(operation == "Injection")
            {
                List<QtyProdResultViewModel> viewModels = new List<QtyProdResultViewModel>()
                {
                    new QtyProdResultViewModel(){Time = "8h-10h", Target = 1260, Line = 1242, ManQuantity = 2},
                    new QtyProdResultViewModel(){Time = "10h10-12h", Target = 1680, Line = 1667, ManQuantity = 2},
                    new QtyProdResultViewModel(){Time = "13h-15h", Target = 1680, Line = 1665, ManQuantity = 2},
                    new QtyProdResultViewModel(){Time = "15h10-17h30", Target = 2100, Line = 0, ManQuantity = 2},
                    new QtyProdResultViewModel(){Time = "18h-20h", Target = 0, Line = 0, ManQuantity = 0},
                };
                Model.ViewModels = viewModels;
            }
            return Model;
        }

        /// <summary>
        /// Tạo trước dữ liệu trống bao gồm 4 dòng
        /// dailyPlan
        /// UPH
        /// UPPH
        /// labor
        /// việc tạo trước 4 dòng giúp khi khai báo ko cần tạo lại nhiều lần
        /// chỉ cần đọc dữ liệu từ dto và gắn vào đây
        /// có lẽ ko dùng cái này nữa :))
        /// </summary>
        /// <returns></returns>
        public List<SVN_targetViewModel> SetUpTargetRows()
        {
            List<SVN_targetViewModel> viewModels = new List<SVN_targetViewModel>() {
                new SVN_targetViewModel(){Item = "Daily Plan"},
                new SVN_targetViewModel(){Item = "UPH"},
                new SVN_targetViewModel(){Item = "UPPH"},
                new SVN_targetViewModel(){Item = "Labor"},
            };
            return viewModels;
        }
    }
}
