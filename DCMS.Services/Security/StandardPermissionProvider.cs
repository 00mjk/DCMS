using DCMS.Core;
using DCMS.Core.Domain.Security;
using System.Collections.Generic;

namespace DCMS.Services.Security
{
    /// <summary>
    /// ��׼Ȩ���ṩ
    /// </summary>
    public partial class StandardPermissionProvider : IPermissionProvider
    {

        #region ����ƽ̨ϵͳ��ʼȨ��

        /// <summary>
        /// ���ʹ�������
        /// </summary>
        public static readonly PermissionRecord AccessAdminPanel = new PermissionRecord
        {
            Name = "���ʹ����̨",
            SystemName = "AccessAdminPanel",
            Module = new Module() { Name = "Standard" }
        };


        /// <summary>
        /// �����ն˹�������
        /// </summary>
        public static readonly PermissionRecord AccessClientAdminPanel = new PermissionRecord
        {
            Name = "�����ն˹�������",
            SystemName = "AccessClientAdminPanel",
            Module = new Module() { Name = "Client" }
        };

        /// <summary>
        /// ����ͻ�ģ��
        /// </summary>
        public static readonly PermissionRecord AllowUserImpersonation = new PermissionRecord
        {
            Name = "�����̨.����ͻ�ģ��",
            SystemName = "AllowUserImpersonation",
            Module = new Module()
            { Name = "Users" }
        };

        /// <summary>
        /// ��Ʒ����
        /// </summary>
        public static readonly PermissionRecord ManageProducts = new PermissionRecord
        {
            Name = "�����̨.��Ʒ����",
            SystemName = "ManageProducts",
            Module = new Module()
            { Name = "Catalog" }
        };

        /// <summary>
        /// �������
        /// </summary>
        public static readonly PermissionRecord ManageCategories = new PermissionRecord
        {
            Name = "�����̨.�������",
            SystemName = "ManageCategories",
            Module = new Module()
            { Name = "Catalog" }
        };


        /// <summary>
        /// �û�����
        /// </summary>
        public static readonly PermissionRecord ManageUsers = new PermissionRecord
        {
            Name = "�����̨.�û�����",
            SystemName = "ManageUsers",
            Module = new Module()
            { Name = "Users" }
        };

        /// <summary>
        /// �û���ɫ����
        /// </summary>
        public static readonly PermissionRecord ManageUserRoles = new PermissionRecord
        {
            Name = "�����̨.�û���ɫ����",
            SystemName = "ManageUserRoles",
            Module = new Module()
            { Name = "Users" }
        };

        /// <summary>
        /// �����̹���
        /// </summary>
        public static readonly PermissionRecord ManageDistributors = new PermissionRecord
        {
            Name = "�����̨.�����̹���",
            SystemName = "ManageDistributors",
            Module = new Module()
            { Name = "Users" }
        };

        /// <summary>
        /// ��Ϣģ��
        /// </summary>
        public static readonly PermissionRecord ManageMessageTemplates = new PermissionRecord
        {
            Name = "�����̨.��Ϣģ��",
            SystemName = "ManageMessageTemplates",
            Module = new Module()
            { Name = "ContentManagement" }
        };

        /// <summary>
        /// ���ù���
        /// </summary>
        public static readonly PermissionRecord ManageSettings = new PermissionRecord
        {
            Name = "�����̨.���ù���",
            SystemName = "ManageSettings",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ������־
        /// </summary>
        public static readonly PermissionRecord ManageActivityLog = new PermissionRecord
        {
            Name = "�����̨.������־",
            SystemName = "ManageActivityLog",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ���ʿ��ƹ���
        /// </summary>
        public static readonly PermissionRecord ManageAcl = new PermissionRecord
        {
            Name = "�����̨. ���ʿ��ƹ���",
            SystemName = "ManageACL",
            Module = new Module()
            { Name = "Configuration" }
        };


        /// <summary>
        /// �����ʼ��˻�
        /// </summary>
        public static readonly PermissionRecord ManageEmailAccounts = new PermissionRecord
        {
            Name = "�����̨.�����ʼ��˻�",
            SystemName = "ManageEmailAccounts",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ����ϵͳ��־
        /// </summary>
        public static readonly PermissionRecord ManageSystemLog = new PermissionRecord
        {
            Name = "�����̨.����ϵͳ��־",
            SystemName = "ManageSystemLog",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ������Ϣ����
        /// </summary>
        public static readonly PermissionRecord ManageMessageQueue = new PermissionRecord
        {
            Name = "�����̨.������Ϣ����",
            SystemName = "ManageMessageQueue",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ����ά��
        /// </summary>
        public static readonly PermissionRecord ManageMaintenance = new PermissionRecord
        {
            Name = "�����̨.����ά��",
            SystemName = "ManageMaintenance",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ����ƻ�����
        /// </summary>
        public static readonly PermissionRecord ManageScheduleTasks = new PermissionRecord
        {
            Name = "�����̨.����ƻ�����",
            SystemName = "ManageScheduleTasks",
            Module = new Module()
            { Name = "Configuration" }
        };

        /// <summary>
        /// ��Ʒ����
        /// </summary>
        public static readonly PermissionRecord ManageAttributes = new PermissionRecord
        {
            Name = "�����̨.������Ʒ����",
            SystemName = "ManageAttributes",
            Module = new Module()
            { Name = "Product" }
        };

        #endregion

        #region ������ϵͳ��ʼȨ��


        public static readonly PermissionRecord PublicStoreAllowNavigation = new PermissionRecord
        {
            Name = "��������",
            SystemName = "PublicStoreAllowNavigation",
            Code = 9998,
            Module = new Module() { Name = "Client" }
        };

        public static readonly PermissionRecord AccessClosedStore = new PermissionRecord
        {
            Name = "���ʹر�վ��",
            SystemName = "AccessClosedStore",
            Code = 9999,
            Module = new Module() { Name = "Client" }
        };

        public static readonly PermissionRecord UserRoleView = new PermissionRecord
        {
            Name = "Ȩ������",
            SystemName = "UserRoleView",
            Code = (int)AccessGranularityEnum.UserRoleView,
            Module = new Module() { Name = "Permission" }
        };

        public static readonly PermissionRecord UserRoleAdd = new PermissionRecord
        {
            Name = "Ȩ�޸���",
            SystemName = "UserRoleAdd",
            Code = (int)AccessGranularityEnum.UserRoleAdd,
            Module = new Module() { Name = "Permission" }
        };


        #endregion

        /// <summary>
        /// ��ȡ����ƽ̨Ȩ��
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<PermissionRecord> GetPermissions()
        {
            return new[]
            {
                AccessAdminPanel,
                AllowUserImpersonation,
                ManageProducts,
                ManageCategories,
                ManageUsers,
                ManageUserRoles,
                ManageDistributors,
                ManageMessageTemplates,
                ManageSettings,
                ManageActivityLog,
                ManageAcl,
                ManageEmailAccounts,
                ManageSystemLog,
                ManageMessageQueue,
                ManageMaintenance,
                ManageScheduleTasks,
                ManageAttributes
            };
        }

        /// <summary>
        /// ��ȡ�����̹���ԱĬ��Ȩ��
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<DefaultPermissionRecord> GetStoreDefaultPermissions()
        {
            return new[]{
                new DefaultPermissionRecord
                {
                    UserRoleSystemName = DCMSDefaults.Administrators,
                    PermissionRecords = new[]
                    {
                        PublicStoreAllowNavigation,
                        AccessClosedStore
                    }
                }
            };
        }

        /// <summary>
        /// ��ȡ��ɫĬ��Ȩ��
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<DefaultPermissionRecord> GetDefaultPermissions()
        {
            return new[]
            {
                new DefaultPermissionRecord
                {
                    UserRoleSystemName = DCMSDefaults.Administrators,
                    PermissionRecords = new[]
                    {
                            AccessAdminPanel,
                            AllowUserImpersonation,
                            ManageProducts,
                            ManageCategories,
                            ManageUsers,
                            ManageUserRoles,
                            ManageDistributors,
                            ManageMessageTemplates,
                            ManageSettings,
                            ManageActivityLog,
                            ManageAcl,
                            ManageEmailAccounts,
                            ManageSystemLog,
                            ManageMessageQueue,
                            ManageMaintenance,
                            ManageScheduleTasks,
                            ManageAttributes
                    }
                },
                new DefaultPermissionRecord
                {
                    UserRoleSystemName = DCMSDefaults.MarketManagers,
                    PermissionRecords = new[]
                    {
                            AccessAdminPanel,
                            AllowUserImpersonation,
                            ManageProducts,
                            ManageCategories,
                            ManageUsers,
                            ManageUserRoles,
                            ManageDistributors,
                            ManageMessageTemplates,
                            ManageSettings,
                            ManageActivityLog,
                            ManageAcl,
                            ManageEmailAccounts,
                            ManageSystemLog,
                            ManageMessageQueue,
                            ManageMaintenance,
                            ManageScheduleTasks,
                            ManageAttributes
                    }
                }
            };
        }
    }
}