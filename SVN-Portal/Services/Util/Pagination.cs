namespace SVN_Portal.Services.Util
{
    public class Pagination
    {
        public List<string> GeneratePagination(int currentPage, int totalPages)
        {
            var pages = new List<string>();

            if (totalPages <= 7)
            {
                for (int i = 1; i <= totalPages; i++)
                    pages.Add(i.ToString());

                return pages;
            }

            pages.Add("1");

            if (currentPage > 4)
                pages.Add("...");

            int start = Math.Max(2, currentPage - 1);
            int end = Math.Min(totalPages - 1, currentPage + 1);

            for (int i = start; i <= end; i++)
                pages.Add(i.ToString());

            if (currentPage < totalPages - 3)
                pages.Add("...");

            pages.Add(totalPages.ToString());

            return pages;
        }
    }
}
