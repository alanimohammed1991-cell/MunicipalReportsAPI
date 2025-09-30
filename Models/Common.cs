namespace MunicipalReportsAPI.Models
{
    public class Common
    {
        public enum ReportStatus
        {
            Submitted = 1,
            InReview = 2,
            InProgress = 3,
            Resolved = 4,
            Closed = 5
        }
        public enum UserRole
        {
            Citizen = 1,
            MunicipalStaff = 2,
            Admin = 3
        }
    }
}
