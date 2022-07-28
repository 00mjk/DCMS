using DCMS.Core.Domain.Users;

namespace DCMS.Services.Authentication
{
    /// <summary>
    /// Authentication service interface
    /// </summary>
    //public partial interface IAuthenticationService 
    //{
    //    /// <summary>
    //    /// Sign in
    //    /// </summary>
    //    /// <param name="user">User</param>
    //    /// <param name="isPersistent">Whether the authentication session is persisted across multiple requests</param>
    //    void SignIn(User user, bool isPersistent);

    //    /// <summary>
    //    /// Sign out
    //    /// </summary>
    //    void SignOut();

    //    /// <summary>
    //    /// Get authenticated user
    //    /// </summary>
    //    /// <returns>User</returns>
    //    User GetAuthenticatedUser();
    //}


    /// <summary>
    /// �����֤����ӿ�
    /// </summary>
    public partial interface IAuthenticationService
    {
        /// <summary>
        /// ����
        /// </summary>
        /// <param name="user">�����־û�cookies</param>
        /// <param name="createPersistentCookie"></param>
        void SignIn(User user, bool isPersistent);
        //void SignIn(User user, bool isPersistent);

        /// <summary>
        /// ע��
        /// </summary>
        void SignOut();

        /// <summary>
        /// ��ȡ����û�
        /// </summary>
        /// <returns></returns>
        User GetAuthenticatedUser();
    }

}