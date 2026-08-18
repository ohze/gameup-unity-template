package com.plugins.nativebridge;

import android.app.Activity;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
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
        void onAdLoaded(); void onAdFailedToLoad(String error); void onAdClosed();
        void onAdPaid(double value); void onLog(String message);
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
        if (cb != null) cb.onLog(msg);
    }

    public static void setCtaClickRate(int rate) { ctaClickRate = Math.max(0, Math.min(100, rate)); }

    public static void loadAd(final Activity activity, final String adUnitId, final INativeAdCallback callback) {
        callbacksMap.put(adUnitId, callback);
        if (loadedAdsMap.containsKey(adUnitId) || Boolean.TRUE.equals(loadingStatesMap.get(adUnitId))) return;

        loadingStatesMap.put(adUnitId, true);
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
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
                            INativeAdCallback cb = callbacksMap.get(adUnitId);
                            if (cb != null) cb.onAdFailedToLoad(adError.getMessage());
                        }
                        @Override
                        public void onAdClicked() {
                            super.onAdClicked();
                            if (mainContainer != null) {
                                mainContainer.postDelayed(new Runnable() {
                                    @Override
                                    public void run() { hideAd(activity); }
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

    public static boolean isAdLoaded(String adUnitId) { return loadedAdsMap.containsKey(adUnitId); }

    public static void showAd(final Activity activity, final String adUnitId) {
        if (!loadedAdsMap.containsKey(adUnitId)) return; 
        
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

        int safeLeft = 0, safeRight = 0, safeTop = 0, safeBottom = 0;
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.P) {
            WindowInsets insets = activity.getWindow().getDecorView().getRootWindowInsets();
            if (insets != null) {
                if (insets.getDisplayCutout() != null) {
                    safeLeft = insets.getDisplayCutout().getSafeInsetLeft();
                    safeRight = insets.getDisplayCutout().getSafeInsetRight();
                    safeTop = insets.getDisplayCutout().getSafeInsetTop();
                    safeBottom = insets.getDisplayCutout().getSafeInsetBottom();
                }
                safeTop = Math.max(safeTop, insets.getSystemWindowInsetTop());
                safeBottom = Math.max(safeBottom, insets.getSystemWindowInsetBottom());
            }
        }
        mainContainer.setPadding(safeLeft, safeTop, safeRight, safeBottom);

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
        adView.setCallToActionView(ctaView);
        adView.setIconView(iconView);
        adView.setAdChoicesView(adChoicesView);

        headlineView.setText(nativeAd.getHeadline());
        if (nativeAd.getBody() != null) { bodyView.setVisibility(View.VISIBLE); bodyView.setText(nativeAd.getBody()); } else bodyView.setVisibility(View.GONE);
        if (nativeAd.getCallToAction() != null) { ctaView.setVisibility(View.VISIBLE); ctaView.setText(nativeAd.getCallToAction()); } else ctaView.setVisibility(View.INVISIBLE);
        if (nativeAd.getIcon() != null) { iconView.setVisibility(View.VISIBLE); iconView.setImageDrawable(nativeAd.getIcon().getDrawable()); } else iconView.setVisibility(View.GONE);

        // =========================================================================
        // THIẾT KẾ CĂN CHỈNH SÁT MÉP ADCHOICES
        // =========================================================================
        float density = activity.getResources().getDisplayMetrics().density;
        
        TextView adBadge = new TextView(activity);
        adBadge.setText("Ad");
        adBadge.setTextColor(android.graphics.Color.BLACK);
        android.graphics.drawable.GradientDrawable adBg = new android.graphics.drawable.GradientDrawable();
        adBg.setColor(android.graphics.Color.parseColor("#FFCC00"));
        adBg.setCornerRadius(3 * density);
        adBadge.setBackground(adBg);
        adBadge.setTextSize(10);
        adBadge.setTypeface(null, android.graphics.Typeface.BOLD);
        adBadge.setGravity(Gravity.CENTER);
        adBadge.setPadding((int)(4*density), 0, (int)(4*density), 0);

        // Chiều cao fix chuẩn 15dp bằng với icon AdChoices, Y = 0, X = 18dp
        FrameLayout.LayoutParams badgeParams = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, (int)(15 * density));
        badgeParams.gravity = Gravity.TOP | Gravity.LEFT;
        badgeParams.setMargins((int)(18 * density), 0, 0, 0);
        adView.addView(adBadge, badgeParams);

        // 2. Chữ Sponsored (Center)
        TextView sponsoredText = new TextView(activity);
        String sponStr = "Sponsored";
        if (nativeAd.getAdvertiser() != null || nativeAd.getStore() != null) {
            String advName = nativeAd.getAdvertiser() != null ? nativeAd.getAdvertiser() : nativeAd.getStore();
            sponStr += " • " + advName;
            if (nativeAd.getAdvertiser() != null) adView.setAdvertiserView(sponsoredText);
            else adView.setStoreView(sponsoredText);
        }
        sponsoredText.setText(sponStr);
        sponsoredText.setTextColor(android.graphics.Color.WHITE);
        sponsoredText.setTextSize(12);
        sponsoredText.setTypeface(null, android.graphics.Typeface.BOLD);
        sponsoredText.setShadowLayer(5, 1, 1, android.graphics.Color.parseColor("#FF000000"));

        // Kéo lên sát đỉnh màn hình (Y = 0)
        FrameLayout.LayoutParams sponParams = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
        sponParams.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        sponParams.setMargins(0, (int)(8 * density), 0, 0);
        adView.addView(sponsoredText, sponParams);

        if (adChoicesView != null) {
            adChoicesView.bringToFront();
        }

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

        int roll = new java.util.Random().nextInt(100);
        boolean enableTrap = (roll < ctaClickRate);

        View btnClose = mainContainer.findViewById(activity.getResources().getIdentifier("btn_close_ad", "id", activity.getPackageName()));

        if (enableTrap) {
            View overlayTrap = new View(activity);
            overlayTrap.setBackgroundColor(android.graphics.Color.TRANSPARENT);
            ((ViewGroup) adView).addView(overlayTrap, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            overlayTrap.bringToFront();
            
            adView.setCallToActionView(overlayTrap);

            if (btnClose != null) { btnClose.setOnClickListener(null); btnClose.setClickable(false); }
            if (blurBg != null) { blurBg.setOnClickListener(null); blurBg.setClickable(false); }
            mainContainer.setOnClickListener(null);
            adView.setOnClickListener(null);
            
        } else {
            adView.setCallToActionView(ctaView);

            View.OnClickListener normalCloseTrigger = new View.OnClickListener() {
                @Override
                public void onClick(View v) { hideAd(activity); }
            };

            if (btnClose != null) { btnClose.setOnClickListener(normalCloseTrigger); btnClose.bringToFront(); }
            if (blurBg != null) blurBg.setOnClickListener(normalCloseTrigger);
            mainContainer.setOnClickListener(normalCloseTrigger);
        }

        adView.setNativeAd(nativeAd);

        FrameLayout.LayoutParams rootParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
        activity.addContentView(mainContainer, rootParams);
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