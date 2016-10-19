using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;
using TsAnalyser.Metrics;

namespace TsAnalyser.Service
{

    [ServiceContract]
    public interface ITsAnalyserApi
    {
        [OperationContract]
        [WebInvoke(Method = "OPTIONS", UriTemplate = "/*")]
        void GetGlobalOptions();

        [OperationContract]
        [WebGet(UriTemplate = "/*")]
        Stream ServeEmbeddedStaticFile();

        [OperationContract]
        [WebGet(UriTemplate = "")]
        Stream ServeIndexEmbeddedStaticFile();

        [OperationContract]
        [WebGet(UriTemplate = "/V1/CurrentMetrics")]
        SerialisableMetrics GetCurrentMetrics();

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/ResetMetrics")]
        void ResetMetrics();
        
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/Start")]
        void StartStream();

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/Stop")]
        void StopStream();

    }


}
