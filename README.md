Intended to be a high performance, thread safe, incredibly simple method of writing text to Console, Debug, and File.
			
Capable of writing directly into GZip, and Zip files.
			
All operations are performed asyncronously on a separate thread, allowing writing massive amounts of text without slowing down application processing

!Important Note!
When flushing large amounts of data, the target output may lag quite a bit. This happens when the process output greatly exceeds the ability of the writer to flush, without blocking the process. As a result, it may appear as though the process is no longer running. It will catch up with itself.

Additional information will be provided on full release