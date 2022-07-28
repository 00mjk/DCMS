using DCMS.Core;
using DCMS.Core.Domain.News;
using System.Collections.Generic;

namespace DCMS.Services.News
{
    /// <summary>
    /// ��ʾ���ŷ���ӿ�
    /// </summary>
    public partial interface INewsService
    {
        #region �������
        /// <summary>
        /// ɾ������
        /// </summary>
        /// <param name="newsItem">News item</param>
        void DeleteNews(NewsItem newsItem);

        /// <summary>
        /// ��ȡ����
        /// </summary>
        /// <param name="newsId">The news identifier</param>
        /// <returns>News</returns>
        NewsItem GetNewsById(int newsId, int? storeId);

        /// <summary>
        /// ��ȡ��һ��
        /// </summary>
        /// <param name="newsId"></param>
        /// <returns></returns>
        NewsItem GetPreNewsById(int newsId, int? storeId);

        /// <summary>
        /// ��ȡ��һ��
        /// </summary>
        /// <param name="newsId"></param>
        /// <returns></returns>
        NewsItem GetNextNewsById(int newsId, int? storeId);

        /// <summary>
        /// ��ȡ�����б�
        /// </summary>
        /// <param name="newsIds"></param>
        /// <returns></returns>
        IList<NewsItem> GetNewsByIds(int[] newsIds);

        /// <summary>
        /// ��ȡָ����������
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        IList<NewsItem> GetNewsByCount(int num);

        /// <summary>
        /// ��ȡȫ������
        /// </summary>
        /// <param name="languageId">Language identifier; 0 if you want to get all records</param>
        /// <param name="storeId">Store identifier; 0 if you want to get all records</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>News items</returns>
        IPagedList<NewsItem> GetAllNews(
            string titile,
            int? categoryId,
             int languageId = 0,
             int storeId = 0,
            int pageIndex = 0,
            int pageSize = int.MaxValue,  //Int32.MaxValue
            bool showHidden = false);


        /// <summary>
        /// �������
        /// </summary>
        /// <param name="news">News item</param>
        void InsertNews(NewsItem news);

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="news">News item</param>
        void UpdateNews(NewsItem news);
        #endregion

        #region �������
        /// <summary>
        /// ��ȡȫ������
        /// </summary>
        /// <param name="customerId">Customer identifier; 0 to load all records</param>
        /// <returns>Comments</returns>
        //IList<NewsComment> GetAllComments(int customerId);

        /// <summary>
        /// ��ȡһ������
        /// </summary>
        /// <param name="newsCommentId">News comment identifier</param>
        /// <returns>News comment</returns>
        //NewsComment GetNewsCommentById(int newsCommentId);

        /// <summary>
        /// ɾ��һ������ 
        /// </summary>
        /// <param name="newsComment">News comment</param>
        //void DeleteNewsComment(NewsComment newsComment);
        #endregion

        #region ����ͼƬ

        ///ɾ��ͼƬ
        void DeleteNewsPicture(NewsPicture newsPicture);
        /// <summary>
        /// ��ȡͼƬ�б�
        /// </summary>
        /// <param name="newsId"></param>
        /// <returns></returns>
        IList<NewsPicture> GetNewsPicturesByNewsId(int newsId);
        /// <summary>
        /// ��ȡ����ͼƬ
        /// </summary>
        /// <param name="newsPictureId"></param>
        /// <returns></returns>
        NewsPicture GetNewsPictureById(int newsPictureId);
        /// <summary>
        /// ����ͼƬ
        /// </summary>
        /// <param name="newsPicture"></param>
        void InsertNewsPicture(NewsPicture newsPicture);
        /// <summary>
        /// �޸�ͼƬ
        /// </summary>
        /// <param name="newsPicture"></param>
        void UpdateNewsPicture(NewsPicture newsPicture);

        #endregion
    }
}
