
namespace DCMS.Web.Framework.UI.Paging
{
    /// <summary>
    /// ��ҳ���󼯺�
    /// </summary>
    public interface IPageableModel
    {

        int PageIndex { get; }

        int PageNumber { get; }

        int PageSize { get; }

        int TotalItems { get; }

        int TotalPages { get; }

        int FirstItem { get; }

        int LastItem { get; }

        bool HasPreviousPage { get; }

        bool HasNextPage { get; }
    }


    /// <summary>
    /// ���� <see cref="IPageableModel"/>
    /// </summary>
    /// <typeparam name="T">Ҫ��ҳ�Ķ�������</typeparam>
    public interface IPagination<T> : IPageableModel
    {

    }

}