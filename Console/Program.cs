using System.IO;

namespace TrotiNet
{
    public static class Utils
    {
        public static void Log_Init()
        {
            string cfg = @"<?xml version='1.0' encoding='utf-8'?>
<log4net>
  <root>
    <level value='DEBUG' />
    <appender-ref ref='ConsoleLog' />
    <!-- <appender-ref ref='FileLog' /> -->
  </root>

  <logger name='Trotinet'>
    <level value='DEBUG'/>
  </logger>

  <appender name='ConsoleLog' type='log4Net.Appender.ColoredConsoleAppender'>
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='[%t] %-5p %c - %m%n'  />
    </layout>
    <mapping>
      <level value='ERROR' />
      <foreColor value='White' />
      <backColor value='Red' />
    </mapping>
    <mapping>
      <level value='INFO' />
      <foreColor value='White' />
    </mapping>
    <mapping>
      <level value='DEBUG' />
      <foreColor value='Green' />
    </mapping>
  </appender>

  <!--
  <appender name='FileLog' type='log4net.Appender.RollingFileAppender'>
    <file value='__CHANGE_ME__/trotinet.txt' />
    <appendToFile value='true' />
    <maximumFileSize value='1000KB' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='3' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%d [%t] %-5p %c - %m%n'  />
    </layout>
  </appender>
  -->
</log4net>";
            log4net.Config.XmlConfigurator.Configure(
                new MemoryStream(System.Text.Encoding.ASCII.GetBytes(cfg)));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Utils.Log_Init();

            var s = new TcpServer(2000, false);
            //s.Start(ProxyDummyEcho.CreateEchoProxy);
            s.Start(ProxyLogic.CreateProxy);
            while (true)
                System.Threading.Thread.Sleep(1000);
            //s.Stop();
        }
    }
}
