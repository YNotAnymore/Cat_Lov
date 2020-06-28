using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Senparc.CO2NET;
using Senparc.CO2NET.Cache;
using Senparc.CO2NET.RegisterServices;
using Senparc.Weixin;
using Senparc.Weixin.Entities;
using Senparc.Weixin.MP.Containers;
using Senparc.Weixin.MP.Helpers;

namespace TestWechatApi2
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public static SenparcSetting senparcSetting;
        public static SenparcWeixinSetting senparcWeixinSetting;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddControllers().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddMemoryCache();//ʹ�ñ��ػ���Ҫ���
            services.Add(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));//ʹ�� Memcached �� Logger ��Ҫ���


            //Senparc.CO2NET ȫ��ע�ᣨ���룩
            services.AddSenparcGlobalServices(Configuration);

            var SenparcSettingJson = @"{
  }";

            var SenparcWeixinSettingJson = @"{
  }";

            senparcSetting = System.Text.Json.JsonSerializer.Deserialize<SenparcSetting>(SenparcSettingJson);
            senparcWeixinSetting = System.Text.Json.JsonSerializer.Deserialize<SenparcWeixinSetting>(SenparcWeixinSettingJson);

        }

        public static void TestWechat()
        {

            JsApiConfig config = new JsApiConfig();
            config.debug = true;
            config.appId = senparcWeixinSetting.WeixinAppId;
            config.timestamp = "7497879879";
            config.nonceStr = "sadf456asf";
            config.signature = null;
            //config.jsApiList = model.jsApiList.Split(',');
            // ����JSSDK

            string jsToken = JsApiTicketContainer.GetJsApiTicketResult(senparcWeixinSetting.WeixinAppId, true)?.ticket;
            string str1 = $"jsapi_ticket={jsToken}&noncestr={config.nonceStr}&timestamp={config.timestamp}&url=";
            byte[] buffer = Encoding.UTF8.GetBytes(str1); // ��ȡbyte����
            byte[] dataHashed = SHA1.Create().ComputeHash(buffer); //SHA1����
            StringBuilder sb = new StringBuilder();
            foreach (var b in dataHashed)
            {
                sb.Append(b.ToString("x2"));// SHA1 ���ܣ�ת��Сд�ַ���
            }
            config.token = AccessTokenContainer.GetAccessTokenAsync(senparcWeixinSetting.WeixinAppId, true).Result;
            config.signature = sb.ToString(); // ��ȡ��������
            config.jsapi_ticket = jsToken;    // jsapi Token 

            var timestamp = JSSDKHelper.GetTimestamp();
            var nonceStr = JSSDKHelper.GetNoncestr();
            string jsapi_ticket = JsApiTicketContainer.TryGetJsApiTicket(senparcWeixinSetting.WeixinAppId, senparcWeixinSetting.WeixinAppSecret);
            string strurl = string.Empty;
            var signature = JSSDKHelper.GetSignature(jsapi_ticket, nonceStr, timestamp, strurl);

            Console.WriteLine(signature);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // ���� CO2NET ȫ��ע�ᣬ���룡
            IRegisterService register = RegisterService.Start(senparcSetting)

                                                       //�Զ�ɨ���Զ�����չ���棨��ѡһ��
                                                       .UseSenparcGlobal(true)

                                                       //ָ���Զ�����չ���棨��ѡһ��
                                                       //.UseSenparcGlobal(false, () => GetExCacheStrategies(senparcSetting.Value))   
                                                       ;

            AccessTokenContainer.RegisterAsync(senparcWeixinSetting.WeixinAppId, senparcWeixinSetting.WeixinAppSecret).GetAwaiter().GetResult();//���������ע�ᣬ�����ʾ��appidδ��ע��

            try
            {
                TestWechat();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
    }

    public class JsApiConfig
    {
        public bool debug { get; set; }
        public string appId { get; set; }
        public string timestamp { get; set; }
        public string nonceStr { get; set; }
        public string signature { get; set; }
        public string[] jsApiList { get; set; }
        public string jsapi_ticket { get; set; }
        public string token { get; set; }
        public string url { get; set; }
    }
}
