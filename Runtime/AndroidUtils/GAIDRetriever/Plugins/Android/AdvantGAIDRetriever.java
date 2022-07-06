package com.rosdvp.androidutils;

import android.content.Context;
import android.os.Handler;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import com.google.android.gms.ads.identifier.AdvertisingIdClient;


public class AdvantGAIDRetriever {

    public static void getGAID(Context unityContext, IGAIDReceiver receiver) {
        ExecutorService executor = Executors.newSingleThreadExecutor();
        Handler handler = new Handler();

        executor.execute(() -> {
            //Background work here
            AdvertisingIdClient.Info idInfo = null;
            String rawId = null;
            try {
                idInfo = AdvertisingIdClient.getAdvertisingIdInfo(unityContext);
                rawId = idInfo.getId();
            } catch (Exception e) {
                e.printStackTrace();
            }
            final String id = rawId;

            handler.post(() -> {
                //UI Thread work here
                receiver.OnGAIDReceived(id);
            });
        });
    }
   
    public interface IGAIDReceiver {
        void OnGAIDReceived(String id);
    }
}