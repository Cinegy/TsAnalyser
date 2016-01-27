using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace TsAnalyser
{

    [ServiceContract]
    public interface ITsAnalyserApi
    {
        [OperationContract]
        [WebInvoke(Method = "OPTIONS", UriTemplate = "/*")]
        void GetGlobalOptions();

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/V1/CurrentMetrics")]
        SerialisableMetrics GetCurrentMetrics();

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/ResetMetrics")]
        void ResetMetrics(string itemPath);
        
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/Start")]
        void StartStream();

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/Stop")]
        void StopStream();

    }


}
