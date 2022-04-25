﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;


namespace Servidores
{

    public class Servidor_TCP
    {
        private struct InformacionDelCliente
        {
            public Socket SK_Cliente;
            public Thread TH_Cliente;
            public byte[] UltimosDatos;
        }
    // declaro el escuchador de TCP que esperara a los clientes 
    private TcpListener TCP_Listener; 
    // declaro la tabla que guardara los datos de los clientes conectados
    private Hashtable ListadoDeClientes = new Hashtable();
    // declaro el thread que esperará a los clientes
    private Thread Thread_TCPListener; 
    // delcaro el endpoint del cliente actual
    private IPEndPoint ID_ClienteActual;



    #region " ************** Declaracion de Eventos ************************"   
        public delegate void EV_NuevaConexion(IPEndPoint ID_Terminal);
        public event EV_NuevaConexion NuevaConexion;

        public delegate void EV_DatosRecibidos(IPEndPoint ID_Terminal, Byte[] Datos, String Datos_str);
        public event EV_DatosRecibidos DatosRecibidos;

        public delegate void EV_ConexionTerminada(IPEndPoint ID_Terminal);
        public event EV_ConexionTerminada ConexionTerminada;

        public delegate void EV_Error_Servidor(Exception ex);
        public event EV_Error_Servidor Error_Servidor;

    #endregion

    #region " ******************** Propiedades *****************************"
        private Int32 TamBuff_RX = 100;
        public Int32 TamañoBuffer_Recepcion
        {
            get
            {
                return TamBuff_RX;
            }
            set
            {
                if (value > 0)
                {
                    TamBuff_RX = value;
                }
            }
        }
        private Int32 Puerto;
        public Int32 Puerto_Del_servidor
        {
            get
            {
                return Puerto;
            }
            set
            {
                if ((value > 0) & (value < 99999))
                {
                    Puerto = value;
                }
            }
        }

        private String IPLocal;
        public String IP_Local
        {
            get
            {                
                string hostname = Dns.GetHostName();

                IPAddress[] estehost = Dns.GetHostAddresses(hostname);
                IPLocal = estehost[1].ToString();

                return IPLocal;
            }
        }

        private Boolean Escuchando;
        public Boolean Esperando_Conexiones
        {
            get
            {
                return Escuchando;
            }
        }

        public Hashtable ListadoDeClientesConectados
        {
            get
            {
                return ListadoDeClientes;
            }
        }
    #endregion


    #region " ********************** Metodos *******************************"
        public String Obtener_Informacion_Del_Cliente(IPEndPoint ID_Del_Cliente)
        {
            String tmp;
            String[] I_P = new String[2];
            int n;
            InformacionDelCliente EsteCliente = (InformacionDelCliente)(ListadoDeClientes[ID_Del_Cliente]);;           
            char[] sep = new char[1] {':'};
            I_P = EsteCliente.SK_Cliente.RemoteEndPoint.ToString().Split(sep);

            tmp = "IP: " + I_P[0] + "\n" +
                  "Protocolo: " + EsteCliente.SK_Cliente.ProtocolType.ToString() + "\n";  
                  // + "DNS: " + Dns.GetHostByAddress(I_P[0]);

            return tmp;
        }

        public void EscucharConexiones()
        {
            if (Escuchando == false)
            {
                Escuchando = true;
                Thread_TCPListener = new Thread(EsperarClientes);
                Thread_TCPListener.Name = "TCP Listener";
                Thread_TCPListener.Start();
            }
        }

        public void Detener_EscuchaDeConexiones()
        {
            if (Escuchando == true)
            {
                Escuchando = false;
                TCP_Listener.Stop();
                Thread_TCPListener.Abort();
                Thread_TCPListener = null;
                
                GC.Collect();

                CerrarTodasLasConexiones();
            }
        }

        public void CerrarConexion(IPEndPoint ID_Del_Cliente)
        {
            InformacionDelCliente EsteCliente;
            try
            {
                EsteCliente = (InformacionDelCliente)ListadoDeClientes[ID_Del_Cliente];
                //EsteCliente.SK_Cliente.Close();
                CerrarThread((IPEndPoint)EsteCliente.SK_Cliente.RemoteEndPoint);
                ListadoDeClientes.Remove(ID_Del_Cliente);
                ConexionTerminada((IPEndPoint)ID_Del_Cliente);
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);              
            }
        }

        public void CerrarTodasLasConexiones()
        {
            foreach (InformacionDelCliente EsteCliente in ListadoDeClientes.Values)
            {
                CerrarThread((IPEndPoint)EsteCliente.SK_Cliente.RemoteEndPoint);
            }
            ListadoDeClientes.Clear();
        }

        public void Enviar_Datos(IPEndPoint ID_Del_Cliente, Byte[] Datos)
        {
            InformacionDelCliente EsteCliente;
            try
            {
                EsteCliente = (InformacionDelCliente)(ListadoDeClientes[ID_Del_Cliente]);
                EsteCliente.SK_Cliente.Send(Datos);
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }
        public void Enviar_Datos(IPEndPoint ID_Del_Cliente, String Datos)
        {
            InformacionDelCliente EsteCliente;
            try
            {
                EsteCliente = (InformacionDelCliente)(ListadoDeClientes[ID_Del_Cliente]);
                EsteCliente.SK_Cliente.Send(Encoding.ASCII.GetBytes(Datos));
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }
        public void BroadCast_Datos(Byte[] Datos)
        {
            foreach (InformacionDelCliente EsteCliente in ListadoDeClientes.Values)
            {
                Enviar_Datos((IPEndPoint)EsteCliente.SK_Cliente.RemoteEndPoint, Datos);
            }
        }
        public void BroadCast_Datos(String Datos)
        {
            foreach (InformacionDelCliente EsteCliente in ListadoDeClientes.Values)
            {
                Enviar_Datos((IPEndPoint)EsteCliente.SK_Cliente.RemoteEndPoint, Datos);
            }
        }
    #endregion

    #region " ********************* Funciones ******************************"
        private void EsperarClientes()
        {
            InformacionDelCliente ClienteActual=new InformacionDelCliente();
            try
            {
                TCP_Listener = new TcpListener(IPAddress.Any, Puerto);
                TCP_Listener.Start();
                while (Escuchando==true)
                {
                    ClienteActual.SK_Cliente = TCP_Listener.AcceptSocket();
                    ID_ClienteActual = (IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint;
                    ClienteActual.TH_Cliente = new Thread(LeerSocket);
                    ClienteActual.TH_Cliente.Name = ((IPEndPoint)ID_ClienteActual).Address.ToString();                  
                    lock (this)
                    {
                        ListadoDeClientes.Add(ID_ClienteActual, ClienteActual);
                    }
                    NuevaConexion((IPEndPoint)ID_ClienteActual);
                    ClienteActual.TH_Cliente.Start();
                }
            }
            catch (Exception ex)
            {
                //if (ex == System.Threading.ThreadAbortException)
                //{
                TCP_Listener.Stop();
                TCP_Listener = null;
                GC.Collect();
                //}
            }
        }
        private void LeerSocket()
        {
            IPEndPoint ID_De_Este_Cliente;
            Byte[] DatosRX;
            Byte[] BufferDatos;
            String DatosRX_str;
            int ret = 0;
            InformacionDelCliente EsteCliente;

            ID_De_Este_Cliente = ID_ClienteActual;
            EsteCliente = (InformacionDelCliente)ListadoDeClientes[ID_De_Este_Cliente];
            BufferDatos = new Byte[TamBuff_RX];

            EsteCliente.UltimosDatos = new Byte[TamBuff_RX];

            while (true)
            {
                if (EsteCliente.SK_Cliente.Connected)
                {
                    Array.Clear(BufferDatos, 0, BufferDatos.Length);
                    try
                    {
                        ret = EsteCliente.SK_Cliente.Receive(BufferDatos);
                        if (ret > 0)
                        {
                            DatosRX = new Byte[ret];
                            Array.Copy(BufferDatos, DatosRX, ret);
                            EsteCliente.UltimosDatos = DatosRX;
                            DatosRX_str=Encoding.ASCII.GetString(DatosRX);

                            DatosRecibidos((IPEndPoint)ID_De_Este_Cliente, DatosRX, DatosRX_str);

                        }
                        else
                        {
                            ConexionTerminada((IPEndPoint)ID_De_Este_Cliente);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if ((ex.Message != "Subproceso anulado.") && 
                            (ex.Message != "Se ha forzado la interrupción de una conexión existente por el host remoto"))
                        {
                            Error_Servidor(ex);                            
                        }
                        ConexionTerminada((IPEndPoint)ID_De_Este_Cliente);
                        break;
                    }
                }
            }
            //CerrarThread((IPEndPoint)ID_De_Este_Cliente);
            //CerrarConexion((IPEndPoint)ID_De_Este_Cliente);
            if (EsteCliente.SK_Cliente.Connected == true)
            {
                EsteCliente.SK_Cliente.Close();
            }
            lock (this)
            {
                ListadoDeClientes.Remove(ID_De_Este_Cliente);
            }
        }

        private void CerrarThread(IPEndPoint ID_Del_Cliente)
        {
            InformacionDelCliente EsteCliente;
            EsteCliente = (InformacionDelCliente)ListadoDeClientes[ID_Del_Cliente];
  
            try
            {
                if (EsteCliente.SK_Cliente.Connected == true)
                {
                    EsteCliente.SK_Cliente.Close();
                }
                EsteCliente.TH_Cliente.Abort();
                
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }
    #endregion
    }

    public class ServidorLiviano_TCP
    {
        public struct InformacionDelCliente
        {
            public Socket SK_Cliente;
            public Thread TH_Cliente;
            public byte[] UltimosDatos;
        }
        // declaro el escuchador de TCP que esperara a los clientes 
        private TcpListener TCP_Listener;
        // declaro la tabla que guardara los datos de los clientes conectados
        private InformacionDelCliente ClienteActual = new InformacionDelCliente();
        // declaro el thread que esperará a los clientes
        private Thread Thread_TCPListener;
        // delcaro el endpoint del cliente actual
        private IPEndPoint ID_ClienteActual;



        #region " ************** Declaracion de Eventos ************************"
        public delegate void EV_NuevaConexion(IPEndPoint ID_Terminal);
        public event EV_NuevaConexion NuevaConexion;

        public delegate void EV_DatosRecibidos(IPEndPoint ID_Terminal, Byte[] Datos, String Datos_str);
        public event EV_DatosRecibidos DatosRecibidos;

        public delegate void EV_ConexionTerminada(IPEndPoint ID_Terminal);
        public event EV_ConexionTerminada ConexionTerminada;

        public delegate void EV_Error_Servidor(Exception ex);
        public event EV_Error_Servidor Error_Servidor;

        #endregion

        #region " ******************** Propiedades *****************************"
        private Int32 TamBuff_RX = 100;
        public Int32 TamañoBuffer_Recepcion
        {
            get
            {
                return TamBuff_RX;
            }
            set
            {
                if (value > 0)
                {
                    TamBuff_RX = value;
                }
            }
        }
        private Int32 Puerto;
        public Int32 Puerto_Del_servidor
        {
            get
            {
                return Puerto;
            }
            set
            {
                if ((value > 0) & (value < 99999))
                {
                    Puerto = value;
                }
            }
        }

        private String IPLocal;
        public String IP_Local
        {
            get
            {
                string hostname = Dns.GetHostName();

                IPAddress[] estehost = Dns.GetHostAddresses(hostname);
                IPLocal = estehost[1].ToString();

                return IPLocal;
            }
        }

        private Boolean Escuchando;
        public Boolean Esperando_Conexiones
        {
            get
            {
                return Escuchando;
            }
        }

        public InformacionDelCliente Cliente
        {
            get
            {
                return ClienteActual;
            }
        }
        #endregion


        #region " ********************** Metodos *******************************"
        public String Obtener_Informacion_Del_Cliente(IPEndPoint ID_Del_Cliente)
        {
            String tmp;
            String[] I_P = new String[2];
            int n;
            
            char[] sep = new char[1] { ':' };
            I_P = ClienteActual.SK_Cliente.RemoteEndPoint.ToString().Split(sep);

            tmp = "IP: " + I_P[0] + "\n" +
                  "Protocolo: " + ClienteActual.SK_Cliente.ProtocolType.ToString() + "\n";
            // + "DNS: " + Dns.GetHostByAddress(I_P[0]);

            return tmp;
        }

        public void EscucharConexiones()
        {
            if (Escuchando == false)
            {
                Escuchando = true;
                Thread_TCPListener = new Thread(EsperarClientes);
                Thread_TCPListener.Name = "TCP Listener";
                Thread_TCPListener.Start();
            }
        }

        public void Detener_EscuchaDeConexiones()
        {
            if (Escuchando == true)
            {
                Escuchando = false;
                TCP_Listener.Stop();
                Thread_TCPListener.Abort();
                Thread_TCPListener = null;

                GC.Collect();

                CerrarConexion();
            }
        }

        public void CerrarConexion()
        {
            
            try
            {
                
                //EsteCliente.SK_Cliente.Close();
                CerrarThread((IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint);

                ConexionTerminada((IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }


        public void Enviar_Datos(Byte[] Datos)
        {
            
            try
            {
                
                ClienteActual.SK_Cliente.Send(Datos);
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }
        public void Enviar_Datos(String Datos)
        {
            
            try
            {
                
                ClienteActual.SK_Cliente.Send(Encoding.ASCII.GetBytes(Datos));
            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }

        #endregion

        #region " ********************* Funciones ******************************"
        private void EsperarClientes()
        {
            
            try
            {
                TCP_Listener = new TcpListener(IPAddress.Any, Puerto);
                TCP_Listener.Start();
                while (Escuchando == true)
                {
                    ClienteActual.SK_Cliente = TCP_Listener.AcceptSocket();
                    ID_ClienteActual = (IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint;
                    ClienteActual.TH_Cliente = new Thread(LeerSocket);
                    ClienteActual.TH_Cliente.Name = ((IPEndPoint)ID_ClienteActual).Address.ToString();

                    NuevaConexion((IPEndPoint)ID_ClienteActual);
                    ClienteActual.TH_Cliente.Start();
                }
            }
            catch (Exception ex)
            {
                //if (ex == System.Threading.ThreadAbortException)
                //{
                TCP_Listener.Stop();
                TCP_Listener = null;
                GC.Collect();
                //}
            }
        }
        private void LeerSocket()
        {
            
            Byte[] DatosRX;
            Byte[] BufferDatos;
            String DatosRX_str;
            int ret = 0;
            
            BufferDatos = new Byte[TamBuff_RX];

            ClienteActual.UltimosDatos = new Byte[TamBuff_RX];

            while (true)
            {
                if (ClienteActual.SK_Cliente.Connected)
                {
                    Array.Clear(BufferDatos, 0, BufferDatos.Length);
                    try
                    {
                        ret = ClienteActual.SK_Cliente.Receive(BufferDatos);
                        if (ret > 0)
                        {
                            DatosRX = new Byte[ret];
                            Array.Copy(BufferDatos, DatosRX, ret);
                            ClienteActual.UltimosDatos = DatosRX;
                            DatosRX_str = Encoding.ASCII.GetString(DatosRX);

                            DatosRecibidos((IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint, DatosRX, DatosRX_str);

                        }
                        else
                        {
                            ConexionTerminada((IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if ((ex.Message != "Subproceso anulado.") &&
                            (ex.Message != "Se ha forzado la interrupción de una conexión existente por el host remoto"))
                        {
                            Error_Servidor(ex);
                        }
                        ConexionTerminada((IPEndPoint)ClienteActual.SK_Cliente.RemoteEndPoint);
                        break;
                    }
                }
            }
            //CerrarThread((IPEndPoint)ID_De_Este_Cliente);
            //CerrarConexion((IPEndPoint)ID_De_Este_Cliente);
            if (ClienteActual.SK_Cliente.Connected == true)
            {
                ClienteActual.SK_Cliente.Close();
            }
        }

        private void CerrarThread(IPEndPoint ID_Del_Cliente)
        {
            

            try
            {
                if (ClienteActual.SK_Cliente.Connected == true)
                {
                    ClienteActual.SK_Cliente.Close();
                }
                ClienteActual.TH_Cliente.Abort();

            }
            catch (Exception ex)
            {
                Error_Servidor(ex);
            }
        }
        #endregion
    }

}
