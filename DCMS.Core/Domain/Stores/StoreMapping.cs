namespace DCMS.Core.Domain.Stores
{
    /// <summary>
    /// ����(������)ʵ��ӳ��
    /// </summary>
    public partial class StoreMapping : BaseEntity
    {
        /// <summary>
        /// ʵ���ʶ
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// ʵ������
        /// </summary>
        public string EntityName { get; set; }


    }
}
