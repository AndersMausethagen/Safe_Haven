param([int]$ProcessId)
# Counts a process's kernel handles by object type (Event, File, Thread, ...).
if (-not ('HC2' -as [type])) {
Add-Type @'
using System;using System.Collections.Generic;using System.Runtime.InteropServices;
public static class HC2{
 [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int c,IntPtr b,int l,out int r);
 [DllImport("ntdll.dll")] static extern int NtQueryObject(IntPtr h,int c,IntPtr b,int l,out int r);
 public static Dictionary<string,int> Count(long pid){
  var names=Types();
  int len=1<<24;IntPtr b;int r;
  while(true){b=Marshal.AllocHGlobal(len);int s=NtQuerySystemInformation(64,b,len,out r);if(s==unchecked((int)0xC0000004)){Marshal.FreeHGlobal(b);len*=2;continue;}break;}
  long n=Marshal.ReadInt64(b);var d=new Dictionary<string,int>();
  for(long i=0;i<n;i++){IntPtr e=new IntPtr(b.ToInt64()+16+i*40);if(Marshal.ReadInt64(e,8)!=pid)continue;
   int t=(ushort)Marshal.ReadInt16(e,30);string k;if(!names.TryGetValue(t,out k))k="type"+t;int c;d.TryGetValue(k,out c);d[k]=c+1;}
  Marshal.FreeHGlobal(b);return d;}
 static Dictionary<int,string> Types(){
  int len=1<<20;IntPtr b=Marshal.AllocHGlobal(len);int r;NtQueryObject(IntPtr.Zero,3,b,len,out r);
  int n=Marshal.ReadInt32(b);var d=new Dictionary<int,string>();long p=b.ToInt64()+8;
  for(int i=0;i<n;i++){IntPtr q=new IntPtr(p);int l=(ushort)Marshal.ReadInt16(q);int ml=(ushort)Marshal.ReadInt16(q,2);
   d[i+2]=Marshal.PtrToStringUni(Marshal.ReadIntPtr(q,8),l/2);p+=0x68+((ml+7)&~7);}
  Marshal.FreeHGlobal(b);return d;}}
'@
}
[HC2]::Count($ProcessId)

