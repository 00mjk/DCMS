namespace DCMS.Core.Domain.Stores
{
    /// <summary>
    /// ����ʵ��ӳ��
    /// </summary>
    public partial interface IStoreMappingSupported
    {
        /// <summary>
        /// �Ƿ�����
        /// </summary>
        bool LimitedToStores { get; set; }
    }
}
