namespace DCMS.Core.Domain.Security
{
    /// <summary>
    /// �����Զ����ʿ���ʶ��
    /// </summary>
    public partial class AclRecord : BaseEntity
    {
        /// <summary>
        /// ʵ���ʶ
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// ʵ����
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// �û���ɫ
        /// </summary>
        public int UserRoleId { get; set; }
    }
}
