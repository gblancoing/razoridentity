namespace ComunaClick.Common.Types;

public sealed record PageRequest(int Page, int PageSize);

public sealed record PageResult<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);
