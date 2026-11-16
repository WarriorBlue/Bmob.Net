namespace Bmob;

/// <summary>
/// REST 路径构造与校验。
/// </summary>
internal static class BmobPaths
{
    internal const string Classes = "/1/classes/";
    internal const string Users = "/1/users";
    internal const string Login = "/1/login";
    internal const string Logout = "/1/logout";
    internal const string Batch = "/1/batch";
    internal const string Functions = "/1/functions/";
    internal const string CloudQuery = "/1/cloudQuery";
    internal const string Files = "/2/files/";
    internal const string Timestamp = "/1/timestamp";

    internal static string Class(string tableName) => Classes + ValidateSegment(tableName, nameof(tableName));

    internal static string ClassRecord(string tableName, string objectId) =>
        Class(tableName) + "/" + ValidateSegment(objectId, nameof(objectId));

    internal static string User(string objectId) => Users + "/" + ValidateSegment(objectId, nameof(objectId));

    internal static string Function(string functionName) => Functions + ValidateSegment(functionName, nameof(functionName));

    internal static string File(string fileName) => Files + Uri.EscapeDataString(ValidateFileName(fileName));

    internal static string FileWithCdn(string cdnName, string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Files + Uri.EscapeDataString(ValidateSegment(cdnName, nameof(cdnName)))
               + "/" + string.Join("/", segments.Select(Uri.EscapeDataString));
    }

    private static string ValidateSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Contains('/') || value.Contains('?') || value.Contains('#'))
        {
            throw new ArgumentException($"'{value}' 不是合法的路径参数。", parameterName);
        }

        return Uri.EscapeDataString(value);
    }

    private static string ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains('?') || fileName.Contains('#'))
        {
            throw new ArgumentException("文件名不能包含路径分隔符。", nameof(fileName));
        }

        return fileName;
    }
}
