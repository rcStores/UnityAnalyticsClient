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

public class AndroidWebRequestExecutor {

    public static void executeWebRequest(
		IWebRequestResultReceiver receiver, 
		String endpoint, 
		String method, 
		String data) throws MalformedURLException, IOException
	{
        ExecutorService executor = Executors.newSingleThreadExecutor();
        Handler handler = new Handler();

        executor.execute(() -> {
			try {
				//Background work here
				HttpResponse result = getRawResponse(endpoint, method, data);

				handler.post(() -> {
					//UI Thread work here
					receiver.OnResultReceived(
						result.data.orElse(null), 
						result.code.orElse(null), 
						result.message.orElse(null), 
						result.error.orElse(null));
				});
			}
			catch (Exception e) {
				e.printStackTrace();
			}
        });
    }
   
    public interface IWebRequestResultReceiver {
        void OnResultReceived(String data, int code, String message, String error);
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

		String CONTENT_TYPE = "application/json";
        connection.setRequestProperty("Content-Type", CONTENT_TYPE);
        connection.setRequestProperty("Connection", "close");
        connection.setRequestMethod(method);

        if (requestBody != null) {
            connection.setDoOutput(true);

            try (OutputStreamWriter writer = new OutputStreamWriter(connection.getOutputStream())) {
                writer.write(requestBody);
            }
        }
        response = new AndroidWebRequestExecutor().new HttpResponse();

        int responseCode = connection.getResponseCode();
        if (responseCode != 200 && responseCode != 201) {
            try(BufferedReader reader = new BufferedReader(
                    new InputStreamReader(connection.getErrorStream(), Charset.forName("utf-8")))) {
                response.error = Optional.of(reader.lines().collect(Collectors.joining(System.lineSeparator())));
            }
        }
        else {
            try(BufferedReader reader = new BufferedReader(
                    new InputStreamReader(connection.getInputStream(), Charset.forName("utf-8")))) {
                response.data = Optional.of(reader.lines().collect(Collectors.joining(System.lineSeparator())));
			}
		}
        response.message = connection.getResponseMessage();
        response.code = responseCode;

        return response;
    }
}