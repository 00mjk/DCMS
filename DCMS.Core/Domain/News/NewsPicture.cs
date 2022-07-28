using DCMS.Core.Domain.Media;

namespace DCMS.Core.Domain.News
{
    /// <summary>
    /// ��ʾ����ͼƬӳ��
    /// </summary>
    public class NewsPicture : BaseEntity
    {

        /// <summary>
        /// ���ű�ʶ
        /// </summary>
        public virtual int NewsItemId { get; set; }

        /// <summary>
        /// ͼƬ��ʶ
        /// </summary>
        public virtual int PictureId { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public virtual int DisplayOrder { get; set; }

        /// <summary>
        /// ��ȡͼƬ
        /// </summary>
        public virtual Picture Picture { get; set; }

        /// <summary>
        /// ��ȡ����
        /// </summary>
        public virtual NewsItem NewsItem { get; set; }
    }

}
