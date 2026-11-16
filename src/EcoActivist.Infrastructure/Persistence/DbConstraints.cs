namespace EcoActivist.Infrastructure.Persistence;

internal static class DbConstraints
{
    public const string UsersEmailUnique = "IX_Users_Email";
    public const string ActiveApplicationUnique = "UX_EventApplications_UserId_EventId_Active";
    public const string AttendanceEventUserUnique = "UX_AttendanceRecords_EventId_UserId";
    public const string EcoPointTransactionAttendanceUnique = "UX_EcoPointTransactions_AttendanceRecordId";
    public const string CategoriesNameUnique = "UX_Categories_Name";
}
