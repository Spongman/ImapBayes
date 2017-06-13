using System;

namespace AE.Net.Mail
{
	public class Attachment : ObjectWHeaders
	{
		public Attachment() { }

		public Attachment(byte[] data, string contentType, string name = null, bool isAttachment = false)
			: this(contentType, name, isAttachment)
		{
			SetBody(data);
		}

		public Attachment(string data, string contentType, string name = null, bool isAttachment = false)
			: this(contentType, name, isAttachment)
		{
			SetBody(data);
		}

		Attachment(string contentType, string name, bool isAttachment)
		{
			this.ContentType = contentType;

			if (!string.IsNullOrEmpty(name))
			{
				var contentDisposition = new HeaderValue(isAttachment ? "attachment" : "inline");
				Headers.Add("Content-Disposition", contentDisposition);
				contentDisposition[isAttachment ? "filename" : "name"] = name;
			}
		}

		public virtual string Filename => Headers["Content-Disposition"]["filename"].NotEmpty(
													Headers["Content-Disposition"]["name"],
													Headers["Content-Type"]["filename"],
													Headers["Content-Type"]["name"]);

		string _contentDisposition;
		string ContentDisposition => _contentDisposition ?? (_contentDisposition = Headers["Content-Disposition"].Value.ToLower());
		public virtual bool OnServer { get; internal set; }

		internal bool IsAttachment => ContentDisposition == "attachment" || !string.IsNullOrEmpty(Filename);
		public virtual void Save(string filename)
		{
			using (var file = new System.IO.FileStream(filename, System.IO.FileMode.Create))
				Save(file);
		}

		public virtual void Save(System.IO.Stream stream)
		{
			var data = GetData();
			stream.Write(data, 0, data.Length);
		}

		public virtual byte[] GetData()
		{
			byte[] data;
			var body = Body;
			if (ContentTransferEncoding.Is("base64") && Utilities.IsValidBase64String(ref body))
			{
				try
				{
					data = Convert.FromBase64String(body);
				}
				catch (Exception)
				{
					data = Encoding.GetBytes(body);
				}
			}
			else
			{
				data = Encoding.GetBytes(body);
			}
			return data;
		}
	}
}