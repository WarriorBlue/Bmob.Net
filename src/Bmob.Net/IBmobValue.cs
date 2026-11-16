using System.Text.Json;

namespace Bmob;

/// <summary>
/// 表示能够按 Bmob 特殊数据类型格式（<c>__type</c>）序列化的值。
/// </summary>
public interface IBmobValue
{
    /// <summary>
    /// 将当前值写入 JSON。
    /// </summary>
    /// <param name="writer">目标 JSON 写入器。</param>
    void WriteTo(Utf8JsonWriter writer);
}
