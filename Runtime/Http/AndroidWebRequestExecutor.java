package com.advant.androidutils;

import android.content.Context;
import android.os.Handler;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.Optional;
import java.io.BufferedReader;
import java.io.IOException;
import java.net.MalformedURLException;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.Charset;
import java.util.stream.Collectors;
import java.io.OutputStreamWriter;
import android.util.Log;
import android.os.Looper;

public class AndroidWebRequestExecutor {

    public static void executeWebRequest(
		IWebRequestResultReceiver receiver, 
		String endpoint, 
		String method, 
		String data) throws MalformedURLException, IOException
	{
		Log.w("Unity", "start request execution");
        ExecutorService executor = Executors.newSingleThreadExecutor();
		Looper.prepare();
        Handler handler = new Handler(Looper.myLooper());
		Log.w("Unity", "invoke executor");
        executor.execute(() -> {
			try {
				//Background work here
				Log.w("Unity", "getting response");
				HttpResponse result = getRawResponse(endpoint, method, data);
				Log.w("Unity", "process response");
				handler.post(() -> {
					//UI Thread work here
					Log.w("Unity", "invoke callback");
					receiver.OnResponseReceived(
						result.data.orElse(null), 
						result.code, 
						result.message, 
						result.error.orElse(null));
					Log.w("Unity", "dto is initialized");
				});
			}
			catch (Exception e) {
				handler.post(() -> {
					//UI Thread work here
					Log.w("Unity", String.format("error %s", e.toString()));
					receiver.OnError(e.toString());
				});
			}
        });
    }
   
    public interface IWebRequestResultReceiver {
        void OnResponseReceived(String data, int code, String message, String error);
		void OnError(String error);
    }

    public class HttpResponse
    {
        Optional<String> data;
        int code;
        String message;
        Optional<String> error;
    }
	
	static HttpResponse response;
	
	private static HttpResponse getRawResponse(String url, String method, String requestBody)
            throws MalformedURLException, IOException {
        HttpURLConnection connection = (HttpURLConnection) new URL(url).openConnection();
		
		Log.w("Unity", "set request headers");
		
		String CONTENT_TYPE = "application/json";
        connection.setRequestProperty("Content-Type", CONTENT_TYPE);
        //connection.setRequestProperty("Connection", "close");
        connection.setRequestMethod(method);

        if (requestBody != null) {
            connection.setDoOutput(true);
			Log.w("Unity", "set request body to post/put");
            try (OutputStreamWriter writer = new OutputStreamWriter(connection.getOutputStream())) {
                writer.write(requestBody);
            }
        }
		Log.w("Unity", "initialize response object");
        response = new AndroidWebRequestExecutor().new HttpResponse();
		
		Log.w("Unity", "connection.getResponseCode()");
		
        int responseCode = connection.getResponseCode();
        if (responseCode != 200 && responseCode != 201) {
			Log.w("Unity", "processing failure");
            try(BufferedReader reader = new BufferedReader(
                    new InputStreamReader(connection.getErrorStream(), Charset.forName("utf-8")))) {
                response.error = Optional.of(reader.lines().collect(Collectors.joining(System.lineSeparator())));
            }
        }
        else {
			Log.w("Unity", "processing success");
            try(BufferedReader reader = new BufferedReader(
                    new InputStreamReader(connection.getInputStream(), Charset.forName("utf-8")))) {
                response.data = Optional.of(reader.lines().collect(Collectors.joining(System.lineSeparator())));
			}
		}
		Log.w("Unity", "getting response message");
        response.message = connection.getResponseMessage();
        response.code = responseCode;
		Log.w("Unity", "returning response");
        return response;
    }
}