namespace DCMS.Core.Domain.Users
{
    /// <summary>
    /// �û���¼Event
    /// </summary>
    public class UserLoggedinEvent
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="user">User</param>
        public UserLoggedinEvent(User user)
        {
            User = user;
        }

        /// <summary>
        /// Customer
        /// </summary>
        public User User
        {
            get;
        }
    }
}