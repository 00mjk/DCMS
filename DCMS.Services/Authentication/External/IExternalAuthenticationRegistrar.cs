using Microsoft.AspNetCore.Authentication;

namespace DCMS.Services.Authentication.External
{
    /// <summary>
    /// ����ע�ᣨ���ã��ⲿ�����֤���񣨲�����Ľӿ�
    /// </summary>
    public interface IExternalAuthenticationRegistrar
    {
        void Configure(AuthenticationBuilder builder);
    }
}
