
namespace DCMS.Core.Domain.Products
{
    /// <summary>
    ///  ��ʾ��Ʒ����
    /// </summary>
    public partial class ProductAttribute : BaseEntity
    {



        /// <summary>
        /// ������
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string Description { get; set; }
    }
}
